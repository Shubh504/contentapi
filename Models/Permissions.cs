using System.Collections.Generic;
using System.Linq;

namespace contentapi.Models
{
    public enum Role
    {
        None = 0,
        SiteAdministrator = 20
    }

    public enum Permission
    {
        CreateCategory,
        UpdateCategory,
        DeleteCategory
    }

    public class PermissionService
    {
        public Dictionary<Role, List<Permission>> ExtraGrants = new Dictionary<Role, List<Permission>>()
        {
            { Role.SiteAdministrator, new List<Permission>() {
                Permission.CreateCategory
            }}
        };

        public List<Permission> GetAllPermissions(Role role)
        {
            return ExtraGrants.Where(x => (int)x.Key <= (int)role).SelectMany(x => x.Value).ToList();
        }

        public bool CanDo(Role role, Permission permission)
        {
            return GetAllPermissions(role).Contains(permission);
        }
    }
}