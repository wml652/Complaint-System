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

        // Add custom Role claim based on the user's Role property
        identity.AddClaim(new Claim(ClaimTypes.Role, user.Role.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Name, user.FullName));

        return identity;
    }
}
