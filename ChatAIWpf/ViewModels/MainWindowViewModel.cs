using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using ChatAIWpf.Clr;
using ChatAIWpf.Models;
using ChatAIWpf.Services;
using ChatAIWpf.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using NLog;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Media;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ChatAIWpf.ViewModels
{
    /// <summary>
    /// 処理結果を示す結果コード
    /// </summary>
    enum VoiceVoxResultCode
    {
        /// <summary>
        /// 成功
        /// </summary>
        VOICEVOX_RESULT_OK,
        /// <summary>
        /// open_jtalk辞書ファイルが読み込まれていない
        /// </summary>
        VOICEVOX_RESULT_NOT_LOADED_OPENJTALK_DICT_ERROR,
        /// <summary>
        /// modelの読み込みに失敗した
        /// </summary>
        VOICEVOX_RESULT_LOAD_MODEL_ERROR,
        /// <summary>
        /// サポートされているデバイス情報取得に失敗した
        /// </summary>
        VOICEVOX_RESULT_GET_SUPPORTED_DEVICES_ERROR,
        /// <summary>
        /// GPUモードがサポートされていない
        /// </summary>
        VOICEVOX_RESULT_GPU_SUPPORT_ERROR,
        /// <summary>
        /// メタ情報読み込みに失敗した
        /// </summary>
        VOICEVOX_RESULT_LOAD_METAS_ERROR,
        /// <summary>
        /// ステータスが初期化されていない
        /// </summary>
        VOICEVOX_RESULT_UNINITIALIZED_STATUS_ERROR,
        /// <summary>
        /// 無効なspeaker_idが指定された
        /// </summary>
        VOICEVOX_RESULT_INVALID_SPEAKER_ID_ERROR,
        /// <summary>
        /// 無効なmodel_indexが指定された
        /// </summary>
        VOICEVOX_RESULT_INVALID_MODEL_INDEX_ERROR,
        /// <summary>
        /// 推論に失敗した
        /// </summary>
        VOICEVOX_RESULT_INFERENCE_ERROR,
        /// <summary>
        /// コンテキストラベル出力に失敗した
        /// </summary>
        VOICEVOX_RESULT_EXTRACT_FULL_CONTEXT_LABEL_ERROR,
        /// <summary>
        /// 無効なutf8文字列が入力された
        /// </summary>
        VOICEVOX_RESULT_INVALID_UTF8_INPUT_ERROR,
        /// <summary>
        /// aquestalk形式のテキストの解析に失敗した
        /// </summary>
        VOICEVOX_RESULT_PARSE_KANA_ERROR,
        /// <summary>
        /// 無効なAudioQuery
        /// </summary>
        VOICEVOX_RESULT_INVALID_AUDIO_QUERY_ERROR
    }

    /// <summary>
    /// MainWindow.xamlのViewModelクラス
    /// </summary>
    public partial class MainWindowViewModel : ObservableObject
    {
        #region プロパティ
        /// <summary>
        /// コンボボックスに表示する入力デバイス一覧
        /// </summary>
        [ObservableProperty]
        private MMDeviceCollection? _audioDevices;

        /// <summary>
        /// 選択された入力デバイス
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CaptureAudioCommand))]
        private MMDevice? _selectedAudioDevice;

        /// <summary>
        /// 録音中フラグ
        /// </summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CaptureButtonContent))]
        [NotifyCanExecuteChangedFor(nameof(CaptureAudioCommand))]
        private bool _isRecording = false;

        /// <summary>
        /// 
        /// </summary>
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(CaptureAudioCommand))]
        private bool _isLoaded = false;

        /// <summary>
        /// 
        /// </summary>
        [ObservableProperty]
        private string _statusBarMessage = string.Empty;

        /// <summary>
        /// 会話した内容
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<string> _conversation = new();

        /// <summary>
        /// キャプチャボタンの文言
        /// </summary>
        public string CaptureButtonContent => IsRecording ? "話してください..." : "話す";
        #endregion

        #region メンバ変数
        /// <summary>
        /// VoiceVoxをCLRで使うためのラッパークラス
        /// </summary>
        private VoiceVoxWrapper _wrapper;
        /// <summary>
        /// ロガー
        /// </summary>
        private readonly ILogger _logger = LogManager.GetCurrentClassLogger();
        /// <summary>
        /// オーディオサービス
        /// </summary>
        private readonly IAudioService _audioService;
        /// <summary>
        /// オーディオ出力クラス
        /// </summary>
        private AudioOutputWriter? _audioOutputWriter;
        /// <summary>
        /// OpenAIサービスインスタンス
        /// </summary>
        private OpenAIService? _openAIService;
        /// <summary>
        /// 
        /// </summary>
        private List<ChatMessage> _messages = new();
        /// <summary>
        /// 
        /// </summary>
        private SecretClient _secretClient;
        /// <summary>
        /// 
        /// </summary>
        private SpeechConfig _speechConfig;
        /// <summary>
        /// Azure Key VaultのキーコンテナーURI
        /// </summary>
        private readonly string _azureKeyVaultUri = Properties.Settings.Default.AzureKeyVaultUri;
        #endregion

        #region コンストラクタ
        /// <summary>
        /// コンストラクタ
        /// </summary>
        public MainWindowViewModel()
        {
            _logger.Info("start");
            _audioService = new AudioService();

            AudioDevices = _audioService.GetActiveCapture();
            SelectedAudioDevice = AudioDevices.FirstOrDefault();
            _logger.Info("end");
        }
        #endregion


        #region コマンド
        /// <summary>
        /// Window読み込み完了時のコマンド
        /// </summary>
        /// <returns></returns>
        [RelayCommand]
        private async Task Loaded()
        {
            await Task.Run(() =>
            {
                StatusBarMessage = "OpenAIの初期化中...";

                // Get secrets.
                _secretClient = new SecretClient(new Uri(_azureKeyVaultUri), new DefaultAzureCredential());
                var azureSpeechApiKey = _secretClient.GetSecretAsync("AzureSpeechAPIKey").Result;
                var openAIApiKey = _secretClient.GetSecretAsync("OpenAIApiKey").Result;

                // Create instances.
                _speechConfig = SpeechConfig.FromSubscription(azureSpeechApiKey.Value.Value, "eastus");
                _speechConfig.SpeechRecognitionLanguage = "ja-JP";

                _openAIService = new OpenAIService(new OpenAiOptions()
                {
                    ApiKey = openAIApiKey.Value.Value,
                });

                _messages.Add(ChatMessage.FromSystem("あなたは日本語で会話ができるチャットボットです。"));

                StatusBarMessage = "VoiceVoxの初期化中...";

                // VoiceVoxの初期化
                _wrapper = new VoiceVoxWrapper("open_jtalk_dic_utf_8-1.11");
                var initRet = ConvertFromInt(_wrapper.Initialize());
                if (initRet != VoiceVoxResultCode.VOICEVOX_RESULT_OK)
                {
                    throw new Exception(initRet.ToString());
                }

                StatusBarMessage = "準備完了";
            });
            IsLoaded = true;
        }

        /// <summary>
        /// 録音開始/停止
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteCaptureAudio))]
        private async Task CaptureAudio()
        {
            _logger.Info("start");
            var audioConfig = AudioConfig.FromMicrophoneInput(SelectedAudioDevice?.ID);
            using (var recognizer = new SpeechRecognizer(_speechConfig, audioConfig))
            {
                IsRecording = true;
                var result = await recognizer.RecognizeOnceAsync();
                IsRecording = false;

                // Checks result.
                if (result.Reason == ResultReason.RecognizedSpeech)
                {
                    Conversation.Add($"あなた: {result.Text}");
                    _messages.Add(ChatMessage.FromUser(result.Text));

                    var completionResult = await _openAIService!.ChatCompletion.CreateCompletion(new ChatCompletionCreateRequest()
                    {
                        Messages = _messages,
                        Model = "gpt-3.5-turbo",
                        MaxTokens = 150,
                    });

                    if (completionResult.Successful)
                    {
                        Conversation.Add($"ChatGPT: {completionResult.Choices.First().Message.Content}");
                        _messages.Add(ChatMessage.FromAssistant(completionResult.Choices.First().Message.Content));

                        // VoiceVoxで再生
                        var ret = ConvertFromInt(_wrapper.GenerateVoice(completionResult.Choices.First().Message.Content));
                        if (ret != VoiceVoxResultCode.VOICEVOX_RESULT_OK)
                        {
                            Conversation.Add($"[SYSTEM] ERROR: VoiceVox handled error.");
                            Conversation.Add($"[SYSTEM] ResultCode={ret}");
                        }

                        var player = new SoundPlayer(@"./speech.wav");
                        player.Play();
                    }
                    else if (completionResult.Error != null)
                    {
                        Conversation.Add($"[SYSTEM] ERROR: Chat GPT handled error.");
                        Conversation.Add($"[SYSTEM] Code={completionResult.Error.Code}");
                        Conversation.Add($"[SYSTEM] Message={completionResult.Error.Message}");
                    }
                    else
                    {
                        Conversation.Add($"[SYSTEM] ERROR: Unknown error occurred at Chat GPT.");
                    }
                }
                else if (result.Reason == ResultReason.NoMatch)
                {
                    Conversation.Add($"[SYSTEM] NOMATCH: Speech could not be recognized.");
                }
                else if (result.Reason == ResultReason.Canceled)
                {
                    var cancellation = CancellationDetails.FromResult(result);
                    Conversation.Add($"[SYSTEM] CANCELED: Reason={cancellation.Reason}");

                    if (cancellation.Reason == CancellationReason.Error)
                    {
                        Conversation.Add($"[SYSTEM] CANCELED: ErrorCode={cancellation.ErrorCode}");
                        Conversation.Add($"[SYSTEM] CANCELED: ErrorDetails={cancellation.ErrorDetails}");
                        Conversation.Add($"[SYSTEM] CANCELED: Did you update the subscription info?");
                    }
                }
            }
            _logger.Info("end");
        }

        /// <summary>
        /// 録音開始/停止ボタンが押下可能か判定
        /// </summary>
        /// <returns></returns>
        private bool CanExecuteCaptureAudio()
        {
            return SelectedAudioDevice != null && !IsRecording && IsLoaded;
        }
        #endregion

        #region メンバメソッド
        /// <summary>
        /// int -> VoiceVoxResultCode
        /// </summary>
        /// <param name="number"></param>
        /// <returns></returns>
        private VoiceVoxResultCode ConvertFromInt(int number)
        {
            return (VoiceVoxResultCode)Enum.ToObject(typeof(VoiceVoxResultCode), number);
        }
        #endregion

        // TODO:
        //  - 誰でもこのアプリからシークレットにアクセスできるようにする
        //  - ListViewにScrollToBottomBehaviorをつける
        //  - ソースをいい感じに切り分ける
        //  - UIをいい感じにする
    }
}
