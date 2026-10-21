using Plannr.Core.Capture;

namespace Plannr.Tests.Capture;

public sealed class ImageSignatureTests
{
    [Fact]
    public void Png() =>
        Assert.Equal("image/png", ImageSignature.Detect([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1]));

    [Fact]
    public void Jpeg() =>
        Assert.Equal("image/jpeg", ImageSignature.Detect([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10]));

    [Fact]
    public void WebP() =>
        Assert.Equal("image/webp", ImageSignature.Detect("RIFF$\0\0\0WEBPVP8 "u8));

    [Theory]
    [InlineData(new byte[0])]
    [InlineData(new byte[] { 0x25, 0x50, 0x44, 0x46 })]        // %PDF
    [InlineData(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39 })]  // GIF89
    [InlineData(new byte[] { 0x89, 0x50, 0x4E })]              // a PNG cut short
    public void AnythingElse_IsNull(byte[] bytes) => Assert.Null(ImageSignature.Detect(bytes));
}
