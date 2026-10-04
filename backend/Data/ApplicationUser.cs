using Microsoft.AspNetCore.Identity;

namespace Backend.Data;

// 現時点で追加列は無いが、テナント所属(TenantMembership)などのナビゲーションを後から追加できるよう専用の型にしておく。
public class ApplicationUser : IdentityUser
{
}
