using NAudio.Wave;
using System;
using System.IO;

namespace ChatAIWpf.Models
{
    /// <summary>
    /// 
    /// </summary>
    public sealed class AudioOutputWriter : IDisposable
    {
        #region イベント
        /// <summary>
        /// 
        /// </summary>
        public event EventHandler<WaveInEventArgs>? DataAvailable;
        #endregion

        #region フィールド
        /// <summary>
        /// 
        /// </summary>
        private readonly string _fileName;
        /// <summary>
        /// 
        /// </summary>
        private readonly WaveInEvent _waveInEvent;
        /// <summary>
        /// 
        /// </summary>
        private Stream? _stream;
        /// <summary>
        /// 
        /// </summary>
        private WaveFileWriter? _waveFileWriter;
        #endregion

        #region コンストラクタ
        public AudioOutputWriter(string fileName, int deviceNumber)
        {
            _fileName = fileName;
            _waveInEvent = new WaveInEvent
            {
                DeviceNumber = deviceNumber
            };
            _waveInEvent.DataAvailable += WaveInOnDataAvailable;
            _waveInEvent.RecordingStopped += WaveInOnRecordingStopped;

            _stream = new FileStream(_fileName, FileMode.Create, FileAccess.Write, FileShare.Write);
            _waveFileWriter = new WaveFileWriter(_stream, _waveInEvent.WaveFormat);
        }
        #endregion

        #region プロパティ
        /// <summary>
        /// 
        /// </summary>
        public bool IsRecording { get; private set; }
        #endregion

        #region メソッド
        /// <summary>
        /// 
        /// </summary>
        public void Start()
        {
            IsRecording = true;
            _waveInEvent.StartRecording();
        }

        /// <summary>
        /// 
        /// </summary>
        public void Stop()
        {
            IsRecording = false;
            _waveInEvent.StopRecording();
        }
        #endregion

        #region イベントハンドラ
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WaveInOnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            if (_waveFileWriter != null)
            {
                _waveFileWriter.Close();
                _waveFileWriter = null;
            }

            if (_stream != null)
            {
                _stream.Close();
                _stream = null;
            }

            Dispose();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void WaveInOnDataAvailable(object? sender, WaveInEventArgs e)
        {
            _waveFileWriter?.Write(e.Buffer, 0, e.BytesRecorded);
            DataAvailable?.Invoke(this, e);
        }
        #endregion

        #region IDisposable
        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            _waveInEvent.DataAvailable -= WaveInOnDataAvailable;
            _waveInEvent.RecordingStopped -= WaveInOnRecordingStopped;

            _waveInEvent?.Dispose();
            _waveFileWriter?.Dispose();
            _stream?.Dispose();
        }
        #endregion
    }
}
