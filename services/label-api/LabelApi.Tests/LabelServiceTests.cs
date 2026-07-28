using TrackNTrash.LabelApi.Gs1;
using TrackNTrash.LabelApi.Models;
using TrackNTrash.LabelApi.Options;
using TrackNTrash.LabelApi.Services;
using Xunit;

namespace TrackNTrash.LabelApi.Tests;

public class LabelServiceTests
{
    private static LabelService NewService(SsccOptions? sscc = null)
    {
        var qr = new QrImageService();
        var zpl = new ZplRenderer(new ZplOptions());
        return new LabelService(new InMemorySerialNumberProvider(), qr, zpl, sscc ?? new SsccOptions());
    }

    [Fact]
    public async Task Carton_labels_have_valid_gs1_and_png()
    {
        var svc = NewService();
        var labels = await svc.CreateCartonLabelsAsync(new CartonLabelRequest
        {
            Gtin = "09501234567891", Quantity = 3, IncludeZpl = true
        });

        Assert.Equal(3, labels.Count);
        Assert.Equal(3, labels.Select(l => l.Serial).Distinct().Count()); // unique serials
        foreach (var l in labels)
        {
            Assert.Equal(14, l.Gtin.Length);
            Assert.StartsWith("(01)", l.Gs1ElementString);
            Assert.Contains("(21)", l.Gs1ElementString);
            Assert.StartsWith("01", l.QrPayload);
            Assert.False(string.IsNullOrEmpty(l.PngBase64));
            Assert.NotNull(l.Zpl);
            Assert.Contains("^XA", l.Zpl);
        }
    }

    [Fact]
    public async Task Carton_rejects_invalid_gtin_check_digit()
    {
        var svc = NewService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateCartonLabelsAsync(new CartonLabelRequest { Gtin = "09501234567890", Quantity = 1 }));
    }

    [Fact]
    public async Task Sscc_labels_are_18_digits_with_valid_check_digit()
    {
        var svc = NewService(new SsccOptions { CompanyPrefix = "0614141", ExtensionDigit = 0 });
        var labels = await svc.CreateSsccLabelsAsync(new SsccLabelRequest { Quantity = 2 });

        Assert.Equal(2, labels.Count);
        foreach (var l in labels)
        {
            Assert.Equal(18, l.Sscc.Length);
            Assert.True(Gs1CheckDigit.IsValid(l.Sscc));
            Assert.StartsWith("(00)", l.Gs1ElementString);
        }
        Assert.NotEqual(labels[0].Sscc, labels[1].Sscc); // monotonic serials -> distinct
    }

    [Fact]
    public async Task Tray_labels_follow_naming_and_optional_svg()
    {
        var svc = NewService();
        var labels = await svc.CreateTrayLabelsAsync(new TrayLabelRequest
        {
            SiteCode = "ldn1", Quantity = 2, LaserEtchSvg = true
        });

        Assert.All(labels, l => Assert.Matches(@"^TRAY-LDN1-\d{6}$", l.TrayQr));
        Assert.All(labels, l => Assert.NotNull(l.Svg));
        Assert.All(labels, l => Assert.Contains("<svg", l.Svg!));
    }

    [Fact]
    public async Task Tray_without_site_code_is_rejected()
    {
        var svc = NewService();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateTrayLabelsAsync(new TrayLabelRequest { SiteCode = "", Quantity = 1 }));
    }
}
