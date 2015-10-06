using System.Collections;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Security.Principal;
using System.Web;
using ProductivityAnalysisSystem.Models;

namespace ProductivityAnalysisSystem
{
    public class ActiveDirectoryResource
    {
        public const string FilterName = "kc-ap-pas";

        // Many functions in this file are disabled by Sarah because 
        // Active Directory doesn't work right on non-enterprise 
        // machines. Re-enable these back on Commerce's network.


        //public static bool AuthenticateUser(string userName, string password, string domain)
        //{
        //    var authentic = false;
        //    try
        //    {
        //        var entry = new DirectoryEntry("LDAP://" + domain,
        //            userName, password);
        //        authentic = true;
        //    }
        //    catch (DirectoryServicesCOMException)
        //    {
        //    }
        //    return authentic;
        //}

        //public static ArrayList GetGroups()
        //{
        //    var groupsList = new ArrayList();
        //    var groups = GetGroupsWithGuids();

        //    foreach (var group in groups)
        //    {
        //        groupsList.Add(group.Value);
        //    }

        //    return groupsList;
        //}

        //public static Dictionary<string, string> GetGroupsWithGuids()
        //{
            
        //    var groups = new Dictionary<string, string>();
        //    var context = HttpContext.Current.Request.LogonUserIdentity;
        //    if (context == null || context.Groups == null) return groups;
        //    foreach (var group in context.Groups)
        //    {
        //        try
        //        {
        //            var groupName = @group.Translate(typeof (NTAccount)).ToString();
        //            if (groupName.Contains(FilterName) && !groupName.EndsWith(groupName))
        //                groups.Add(group.Value, groupName);
        //        }
        //        catch (IdentityNotMappedException)
        //        {
                    
        //        }
        //    }
        //    return groups;
        //}

        public static string GetUsername()
        {
            var username = (HttpContext.Current.Request.LogonUserIdentity != null)
                ? HttpContext.Current.Request.LogonUserIdentity.Name
                : "";
            return username.Substring(0,3);
        }

        public static string GetFirstName()
        {
            //UserPrincipal userPrincipal = UserPrincipal.Current;
            //return userPrincipal.GivenName;
            return "Firstname";
        }

        public static string GetLastName()
        {
            //UserPrincipal userPrincipal = UserPrincipal.Current;
            //return userPrincipal.Surname;
            return "Lastname";
        }

        public static string GetFullName()
        {
            return GetFirstName() + " " + GetLastName();
        }

        public static string GetDisplayName()
        {
            //UserPrincipal userPrincipal = UserPrincipal.Current;
            //return userPrincipal.DisplayName;
            return GetFullName();
        }

        public static string GetPhoneNumber()
        {
            //UserPrincipal userPrincipal = UserPrincipal.Current;
            //return userPrincipal.VoiceTelephoneNumber;\
            return "111-111-1111";
        }

    }
}
