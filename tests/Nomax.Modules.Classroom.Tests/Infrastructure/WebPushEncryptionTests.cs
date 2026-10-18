using System.Security.Cryptography;
using System.Text;
using Nomax.Modules.Classroom.Infrastructure.Notifications;

namespace Nomax.Modules.Classroom.Tests.Infrastructure;

/// <summary>RFC 8291 Appendix A: the web push encryption must reproduce the published example byte for byte.</summary>
public sealed class WebPushEncryptionTests
{
    private const string Plaintext = "When I grow up, I want to be a watermelon";
    private const string ServerPrivate = "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw";
    private const string ServerPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    private const string ClientPublic = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string AuthSecret = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string Salt = "DGv6ra1nlYgDCS1FRnbzlw";
    private const string Expected =
        "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN";

    [Fact]
    public void Encrypts_the_rfc_8291_example()
    {
        var serverPublic = Base64Url.Decode(ServerPublic);
        using var serverKey = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = Base64Url.Decode(ServerPrivate),
            Q = new ECPoint { X = serverPublic[1..33], Y = serverPublic[33..65] },
        });

        var body = WebPushEncryption.Encrypt(
            Encoding.UTF8.GetBytes(Plaintext),
            Base64Url.Decode(ClientPublic),
            Base64Url.Decode(AuthSecret),
            serverKey,
            Base64Url.Decode(Salt));

        Base64Url.Encode(body).ShouldBe(Expected);
    }

    [Fact]
    public void Base64url_round_trips_without_padding()
    {
        var bytes = RandomNumberGenerator.GetBytes(33);
        var text = Base64Url.Encode(bytes);

        text.ShouldNotContain("=");
        Base64Url.Decode(text).ShouldBe(bytes);
    }
}
