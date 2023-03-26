using Azure.Security.KeyVault.Secrets;
using ChatAIWpf.Models;
using ChatAIWpf.Services;
using ChatAIWpf.Services.Interfaces;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using NAudio.CoreAudioApi;
using OpenAI.GPT3;
using OpenAI.GPT3.Managers;
using OpenAI.GPT3.ObjectModels.RequestModels;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Identity;

namespace ChatAIWpf.ViewModels
{
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
            _audioService = new AudioService();

            AudioDevices = _audioService.GetActiveCapture();
            SelectedAudioDevice = AudioDevices.FirstOrDefault();

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
        }
        #endregion


        #region コマンド
        /// <summary>
        /// 録音開始/停止
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanExecuteCaptureAudio))]
        private async Task CaptureAudio()
        {
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
        }

        /// <summary>
        /// 録音開始/停止ボタンが押下可能か判定
        /// </summary>
        /// <returns></returns>
        private bool CanExecuteCaptureAudio()
        {
            return SelectedAudioDevice != null || IsRecording;
        }
        #endregion

        // TODO:
        //  - ChatGPTの返答をVoiceVoxに突っ込んで再生する
        //  - 誰でもこのアプリからシークレットにアクセスできるようにする
        //  - ListViewにScrollToBottomBehaviorをつける
        //  - ソースをいい感じに切り分ける
        //  - UIをいい感じにする
    }
}
