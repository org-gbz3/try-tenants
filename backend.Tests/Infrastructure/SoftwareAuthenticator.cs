using System.Buffers.Binary;
using System.Buffers.Text;
using System.Formats.Cbor;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Backend.Tests.Infrastructure;

// 実際の認証器の代わりに WebAuthn の応答を組み立てる。ES256 鍵と attestation 形式 none のみを扱う。
public sealed class SoftwareAuthenticator : IDisposable
{
    private const string Origin = TestAppFactory.Origin;

    private readonly ECDsa _key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    private readonly byte[] _credentialId = RandomNumberGenerator.GetBytes(16);
    private byte[]? _userHandle;
    private uint _signCount;

    public string CreateAttestation(string creationOptionsJson)
    {
        var options = JsonNode.Parse(creationOptionsJson)!;
        var rpId = options["rp"]!["id"]!.GetValue<string>();
        _userHandle = Base64Url.DecodeFromChars(options["user"]!["id"]!.GetValue<string>());

        var clientDataJson = ClientDataJson("webauthn.create", options["challenge"]!.GetValue<string>());
        byte[] authenticatorData =
        [
            .. AuthenticatorDataHeader(rpId, flags: 0x45), // UP | UV | AT
            .. new byte[16], // AAGUID
            .. BigEndian((ushort)_credentialId.Length),
            .. _credentialId,
            .. CosePublicKey(),
        ];

        var attestationObject = new CborWriter();
        attestationObject.WriteStartMap(3);
        attestationObject.WriteTextString("fmt");
        attestationObject.WriteTextString("none");
        attestationObject.WriteTextString("attStmt");
        attestationObject.WriteStartMap(0);
        attestationObject.WriteEndMap();
        attestationObject.WriteTextString("authData");
        attestationObject.WriteByteString(authenticatorData);
        attestationObject.WriteEndMap();

        return Credential(new JsonObject
        {
            ["clientDataJSON"] = Base64Url.EncodeToString(clientDataJson),
            ["attestationObject"] = Base64Url.EncodeToString(attestationObject.Encode()),
            ["transports"] = new JsonArray("internal"),
        });
    }

    public string CreateAssertion(string requestOptionsJson)
    {
        var options = JsonNode.Parse(requestOptionsJson)!;
        var rpId = options["rpId"]!.GetValue<string>();

        var clientDataJson = ClientDataJson("webauthn.get", options["challenge"]!.GetValue<string>());
        var authenticatorData = AuthenticatorDataHeader(rpId, flags: 0x05); // UP | UV
        var signature = _key.SignData(
            [.. authenticatorData, .. SHA256.HashData(clientDataJson)],
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);

        return Credential(new JsonObject
        {
            ["clientDataJSON"] = Base64Url.EncodeToString(clientDataJson),
            ["authenticatorData"] = Base64Url.EncodeToString(authenticatorData),
            ["signature"] = Base64Url.EncodeToString(signature),
            ["userHandle"] = _userHandle is null ? null : Base64Url.EncodeToString(_userHandle),
        });
    }

    public void Dispose() => _key.Dispose();

    private string Credential(JsonObject response)
    {
        var id = Base64Url.EncodeToString(_credentialId);
        return new JsonObject
        {
            ["id"] = id,
            ["rawId"] = id,
            ["type"] = "public-key",
            ["response"] = response,
            ["clientExtensionResults"] = new JsonObject(),
            ["authenticatorAttachment"] = "platform",
        }.ToJsonString();
    }

    private static byte[] ClientDataJson(string type, string challenge) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { type, challenge, origin = Origin, crossOrigin = false }));

    private byte[] AuthenticatorDataHeader(string rpId, byte flags) =>
        [.. SHA256.HashData(Encoding.UTF8.GetBytes(rpId)), flags, .. BigEndian(++_signCount)];

    private byte[] CosePublicKey()
    {
        var parameters = _key.ExportParameters(includePrivateParameters: false);
        // CTAP2 の正準順序(1, 3, -1, -2, -3)で書く。
        var writer = new CborWriter(CborConformanceMode.Ctap2Canonical);
        writer.WriteStartMap(5);
        writer.WriteInt32(1); // kty
        writer.WriteInt32(2); // EC2
        writer.WriteInt32(3); // alg
        writer.WriteInt32(-7); // ES256
        writer.WriteInt32(-1); // crv
        writer.WriteInt32(1); // P-256
        writer.WriteInt32(-2); // x
        writer.WriteByteString(parameters.Q.X!);
        writer.WriteInt32(-3); // y
        writer.WriteByteString(parameters.Q.Y!);
        writer.WriteEndMap();
        return writer.Encode();
    }

    private static byte[] BigEndian(ushort value)
    {
        var bytes = new byte[2];
        BinaryPrimitives.WriteUInt16BigEndian(bytes, value);
        return bytes;
    }

    private static byte[] BigEndian(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
