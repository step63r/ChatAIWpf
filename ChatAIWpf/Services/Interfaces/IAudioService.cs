using NAudio.CoreAudioApi;

namespace ChatAIWpf.Services.Interfaces
{
    /// <summary>
    /// オーディオサービス インターフェイス
    /// </summary>
    public interface IAudioService
    {
        /// <summary>
        /// アクティブな入力デバイスを取得する
        /// </summary>
        /// <returns>アクティブな入力デバイス</returns>
        MMDeviceCollection GetActiveCapture();

        /// <summary>
        /// デバイスオブジェクトからデバイス番号を取得する
        /// </summary>
        /// <param name="deivce">デバイスオブジェクト</param>
        /// <returns>デバイス番号</returns>
        int GetDeviceNumber(MMDevice deivce);
    }
}
