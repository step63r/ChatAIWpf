using ChatAIWpf.Services.Interfaces;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;

namespace ChatAIWpf.Services
{
    /// <summary>
    /// オーディオサービス クラス
    /// </summary>
    public sealed class AudioService : IAudioService
    {
        /// <summary>
        /// アクティブな入力デバイスを取得する
        /// </summary>
        /// <returns>アクティブな入力デバイス</returns>
        public MMDeviceCollection GetActiveCapture()
        {
            return new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
        }

        /// <summary>
        /// デバイスオブジェクトからデバイス番号を取得する
        /// </summary>
        /// <param name="device">デバイスオブジェクト</param>
        /// <returns>デバイス番号</returns>
        public int GetDeviceNumber(MMDevice device)
        {
            for (int i = 0; i < WaveIn.DeviceCount; i++)
            {
                var cap = WaveIn.GetCapabilities(i);
                if (cap.ProductName.Equals(device.FriendlyName))
                {
                    return i;
                }
            }
            throw new ArgumentException($"Device number not found: {device.FriendlyName}");
        }
    }
}
