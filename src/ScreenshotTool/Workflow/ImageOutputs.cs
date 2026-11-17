using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace ScreenshotTool.Workflow;

public sealed class ImageOutputs : IImageOutputs
{
    private readonly Action<BitmapSource> _copy;
    public ImageOutputs(Action<BitmapSource>? copy = null) => _copy = copy ?? CopyToSystemClipboard;
    public Task<OutputResult> CopyAsync(BitmapSource image)
    {
        try { _copy(image); return Task.FromResult(OutputResult.Ok()); }
        catch (Exception error) when (error is ExternalException or InvalidOperationException or IOException)
        { return Task.FromResult(OutputResult.Fail("剪贴板暂不可用，请重试。" + error.Message)); }
    }
    public Task<OutputResult> SaveAsync(BitmapSource image, string path, ImageFormat format)
    {
        string? temporary = null;
        try
        {
            var fullPath = Path.GetFullPath(path);
            var parent = Path.GetDirectoryName(fullPath) ?? throw new IOException("保存目录无效。");
            if (!Directory.Exists(parent)) throw new DirectoryNotFoundException("保存目录不存在。");
            temporary = Path.Combine(parent, ".screenshot-" + Guid.NewGuid().ToString("N") + ".tmp");
            BitmapEncoder encoder = format == ImageFormat.Jpeg ? new JpegBitmapEncoder { QualityLevel = 95 } : new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) encoder.Save(stream);
            File.Move(temporary, fullPath, true);
            temporary = null;
            return Task.FromResult(OutputResult.Ok());
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException or InvalidOperationException or ExternalException)
        { return Task.FromResult(OutputResult.Fail("无法保存图片：" + error.Message)); }
        finally
        {
            if (temporary is not null)
            {
                try { File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }
    private static void CopyToSystemClipboard(BitmapSource image)
    {
        var data = new DataObject();
        data.SetImage(image);
        Clipboard.SetDataObject(data, true);
    }
}
