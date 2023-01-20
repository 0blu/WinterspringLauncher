namespace WinterspringLauncher.Utils;

public class ProgressBarPrinter
{
    private const int TOTAL_PROGRESSBAR_LENGTH = 50;
    private readonly string _description;

    private double _lastProgress = 0;
    private DateTime? _lastProgressUpdate = null; 
    private int _lastProgressRatesIdx = 0;
    private readonly double?[] _lastProgressRates = new double?[15];

    public ProgressBarPrinter(string description)
    {
        _description = description;
    }

    /// Will print something like "{description} [████▌              ] 13.5% {additionalText} Est: 00:09:23"
    /// <param name="progress">Value from [0..1]</param>
    public void UpdateState(double progress, string additionalText)
    {
        double blockAmount = progress * TOTAL_PROGRESSBAR_LENGTH;
        int blockCount = (int)blockAmount;
        string left = new String('█', blockCount);
        char middle = SelectMiddleChar(blockAmount - blockCount);
        string right = blockAmount == TOTAL_PROGRESSBAR_LENGTH ? string.Empty : new String(' ', (TOTAL_PROGRESSBAR_LENGTH - blockCount - 1));

        TimeSpan? estimatedTime = GetEstimatedTimeAndUpdateRates(progress);
        string timeLeft = estimatedTime.HasValue
            ? TimeSpan.FromSeconds((long) estimatedTime.Value.TotalSeconds).ToString()
            : "?".PadLeft("00:00:00".Length);

        string line = $"\r{_description} [{left}{middle}{right}]  {(progress * 100).ToString("0.0").PadLeft(5)}%  {additionalText}  Est: {timeLeft}    ";
        Console.Write(line);
    }

    private TimeSpan? GetEstimatedTimeAndUpdateRates(double progress)
    {
        var now = DateTime.Now;
        if (_lastProgressUpdate != null)
        {
            TimeSpan timeDiff = now - _lastProgressUpdate.Value;
            double progressDiff = progress - _lastProgress;
            double progressDiffPerSec = progressDiff / timeDiff.TotalSeconds;
            _lastProgressRates[_lastProgressRatesIdx] = progressDiffPerSec;
            _lastProgressRatesIdx = (_lastProgressRatesIdx + 1) % _lastProgressRates.Length;
        }
        _lastProgressUpdate = now;
        _lastProgress = progress;

        var avgRate = _lastProgressRates.Where(x => x.HasValue).Select(x => x!.Value).DefaultIfEmpty(0).Average();
        if (avgRate == 0)
        {
            return null;
        }

        return TimeSpan.FromSeconds((1 - progress) / avgRate);
    }

    public void Done()
    {
        string blocks = new String('█', TOTAL_PROGRESSBAR_LENGTH);
        string line = $"\r{_description} [{blocks}]  100%   Done                                       ";
        Console.WriteLine(line);
    }

    // amount: [0..1)
    private static char SelectMiddleChar(double amount)
    {
        return amount >= 0.5 ? '▌' : ' ';
    }

    public Stream FromStream(Stream inputStream)
    {
        return new StreamWrapper(inputStream, this);
    }

    private class StreamWrapper : Stream
    {
        private readonly Stream _stream;
        private readonly ProgressBarPrinter _progress;

        public StreamWrapper(Stream stream, ProgressBarPrinter progress)
        {
            _stream = stream;
            _progress = progress;
        }
        
        
        public override void Flush()
        {
            _stream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int actualCount = _stream.Read(buffer, offset, count);
            _progress.UpdateState(_stream.Position / (double)_stream.Length, "");
            return actualCount;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return _stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        { 
            _stream.SetLength(value);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            _stream.Write(buffer, offset, count);
        }

        public override bool CanRead => _stream.CanRead;
        public override bool CanSeek => _stream.CanSeek;
        public override bool CanWrite => _stream.CanWrite;
        public override long Length => _stream.Length;
        public override long Position
        {
            get => _stream.Position;
            set => _stream.Position = value;
        }
    }
}
