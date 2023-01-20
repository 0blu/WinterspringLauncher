using System.Globalization;
using System.Timers;

namespace WinterspringLauncher.Utils;

public class FileDownloader : IDisposable
{
    private readonly string _downloadUrl;
    private readonly string _destinationFilePath;

    private readonly HttpClient _httpClient;

    public delegate void InitialFileInfoHandler(long? totalFileSize);
    public delegate void ProgressFixedChangedHandler(long? totalFileSize, long alreadyReceived, long currentBytesPerSecond);
    public delegate void DownloadDoneHandler();

    public event InitialFileInfoHandler? InitialInfo;
    public event ProgressFixedChangedHandler? ProgressChangedFixedDelay;
    public event DownloadDoneHandler? DownloadDone;

    private readonly System.Timers.Timer _updateTimer;
    private DateTime? _lastUpdateInvoke;
    private long _lastReceivedBytes;
    private long? _totalFileSize;
    private long _alreadyReceivedBytes;

    private int _lastDownloadRatesIdx = 0;
    private readonly double?[] _lastDownloadRates = new double?[15];
    private bool _hadZeroRate = false;

    public FileDownloader(string downloadUrl, string destinationFilePath)
    {
        _downloadUrl = downloadUrl;
        _destinationFilePath = destinationFilePath;
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        _updateTimer = new System.Timers.Timer(500 /*ms*/);
        _updateTimer.Elapsed += TimerElapsed;
    }

    public async Task StartPostUploadFileAndDownload(Stream uploadedFile)
    {
        _httpClient.Timeout = Timeout.InfiniteTimeSpan;
        using var content = new MultipartFormDataContent("Upload----" + DateTime.Now.ToString(CultureInfo.InvariantCulture));

        ProgressBarPrinter uploadProgress = new ProgressBarPrinter("Patching");
        content.Add(new StreamContent(uploadProgress.FromStream(uploadedFile)), "file", "file");

        var request = new HttpRequestMessage(HttpMethod.Post, _downloadUrl);
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        uploadProgress.Done();
        await DownloadFileFromHttpResponseMessage(response);
    }

    public async Task StartGetDownload()
    {
        using (var response = await _httpClient.GetAsync(_downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            await DownloadFileFromHttpResponseMessage(response);
    }

    private async Task DownloadFileFromHttpResponseMessage(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;
        _totalFileSize = totalBytes;

        TriggerInitialInfo(totalBytes);

        await using (var contentStream = await response.Content.ReadAsStreamAsync())
        {
            await ProcessContentStream(contentStream);
        }
    }

    private async Task ProcessContentStream(Stream contentStream)
    {
        long totalBytesRead = 0;
        long readCount = 0;
        var buffer = new byte[4096];
        var isMoreToRead = true;

        _updateTimer.Start();
        try
        {
            using (var fileStream = new FileStream(_destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None, buffer.Length, true))
            {
                do
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        isMoreToRead = false;
                        UpdateInternalProgress(totalBytesRead);
                        continue;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                    readCount += 1;

                    if (readCount % 1000 == 0)
                        UpdateInternalProgress(totalBytesRead);
                } while (isMoreToRead);
            }
        }
        finally
        {
            _updateTimer.Stop();
        }

        TriggerDownloadDone();
    }

    private void TriggerInitialInfo(long? totalDownloadSize)
    {
        InitialInfo?.Invoke(totalDownloadSize);
    }

    private void UpdateInternalProgress(long alreadyReceivedBytes)
    {
        _alreadyReceivedBytes = alreadyReceivedBytes;
    }

    private void TimerElapsed(object? sender, ElapsedEventArgs e)
    {
        UpdateAndTriggerProgressChanged();
    }

    private void UpdateAndTriggerProgressChanged()
    {
        DateTime now = DateTime.Now;
        var elapsed = now - _lastUpdateInvoke;
        long amountDownloadedInPeriod = _alreadyReceivedBytes - _lastReceivedBytes;
        if (amountDownloadedInPeriod == 0 && !_hadZeroRate)
        {
            _hadZeroRate = true;
            return;
        }
        _hadZeroRate = false;
        _lastUpdateInvoke = now;
        _lastReceivedBytes = _alreadyReceivedBytes;

        if (elapsed != null)
        {
            double thisBytePerSec = amountDownloadedInPeriod / elapsed.Value.TotalSeconds;
            _lastDownloadRates[_lastDownloadRatesIdx] = thisBytePerSec;
            _lastDownloadRatesIdx = (_lastDownloadRatesIdx + 1) % _lastDownloadRates.Length;

            TriggerProgressChanged();
        }
    }

    private void TriggerProgressChanged()
    {
        double dlRate = _lastDownloadRates.Where(x => x != null).Select(x => x!.Value).Average();
        ProgressChangedFixedDelay?.Invoke(_totalFileSize, _alreadyReceivedBytes, (long)dlRate);
    }

    private void TriggerDownloadDone()
    {
        DownloadDone?.Invoke();
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        _updateTimer.Elapsed -= TimerElapsed;
        _updateTimer.Stop();
    }
}
