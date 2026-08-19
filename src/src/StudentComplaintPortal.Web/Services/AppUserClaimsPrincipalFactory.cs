using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using StudentComplaintPortal.Domain.Entities;
using System.Security.Claims;

namespace StudentComplaintPortal.Web.Services;

public class AppUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<AppUser, IdentityRole>
{
    public AppUserClaimsPrincipalFactory(
        UserManager<AppUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> options)
        : base(userManager, roleManager, options)
    {
    }

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        // base.GenerateClaimsAsync() Identity-role-assignment (jaise "Query Handler" custom-role,
        // jo Assign-Roles-feature se AddToRoleAsync() ke zariye attach hoti hai) se bhi ek
        // ClaimTypes.Role claim add kar-deta-hai (custom-role-ka-naam) — jo hamare authoritative
        // Student/Staff/Admin role (AppUser.Role enum) ke saath CONFLICT karti thi (duplicate
        // ClaimTypes.Role claims, jisse User.FindFirstValue(ClaimTypes.Role) unpredictable
        // pehli-milne-wali-claim utha leta tha, kabhi custom-role-naam kabhi Staff/Admin/Student).
        // Isliye pehle base-ki-daali-hui Role-claims hata kar sirf apni authoritative-wali rakhte hain.
        // Permission-type claims (jo isi role-assignment se aati hain) ko touch nahi kiya — wo zaroori hain.
        var existingRoleClaims = identity.FindAll(ClaimTypes.Role).ToList();
        foreach (var roleClaim in existingRoleClaims)
        {
            identity.RemoveClaim(roleClaim);
        }

        // Add custom Role claim based on the user's Role property
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.FullName));

        return identity;
    }
}
