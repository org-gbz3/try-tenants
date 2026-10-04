using System.ComponentModel.DataAnnotations;

namespace Backend.Auth;

public class AppOptions
{
    public const string SectionName = "App";

    // メール内のリンクは Host ヘッダーではなくこの値から組み立て、ヘッダー偽装で外部 URL へ誘導されないようにする。
    [Required]
    [Url]
    public string PublicBaseUrl { get; set; } = "";
}
