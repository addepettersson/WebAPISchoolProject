﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Web;
using ProjectApi.Controllers;

namespace ProjectApi.Modules
{
    public class BasicAuthHttpModule : IHttpModule
    {
        private const string Realm = "ProjectApi";

        public void Init(HttpApplication context)
        {
            // Register event handlers
            context.AuthenticateRequest += OnApplicationAuthenticateRequest;
            context.EndRequest += OnApplicationEndRequest;
        }

        private static void SetPrincipal(IPrincipal principal)
        {
            Thread.CurrentPrincipal = principal;
            if (HttpContext.Current != null)
            {
                HttpContext.Current.User = principal;
            }
        }

        // TODO: Here is where you would validate the username and password.
        private static bool CheckPassword(string username, string password)
        {
            try
            {
                using (var db = new WpfprojectEntities())
                {
                    string userSalt = db.User.Where(x => x.UserName == username).Select(x => x.Salt).SingleOrDefault();
                    string hashedPassword;

                    using (MD5 md5Hash = MD5.Create())
                    {
                        hashedPassword = UserController.GetMd5Hash(md5Hash, password + userSalt);
                    }
                    if (db.User.Any(x => x.UserName == username && x.Password == hashedPassword))
                    {
                        return true;
                    }
                    return false;
                }
            }
            catch
            {
                return false;

            }

        }

        private static void AuthenticateUser(string credentials)
        {
            try
            {
                var encoding = Encoding.GetEncoding("iso-8859-1");
                credentials = encoding.GetString(Convert.FromBase64String(credentials));

                int separator = credentials.IndexOf(':');
                string name = credentials.Substring(0, separator);
                string password = credentials.Substring(separator + 1);

                if (CheckPassword(name, password))
                {
                    using (var db = new WpfprojectEntities())
                    {
                        string userSalt = db.User.Where(x => x.UserName == name).Select(x => x.Salt).SingleOrDefault();
                        string hashedPassword;

                        using (MD5 md5Hash = MD5.Create())
                        {
                            hashedPassword = UserController.GetMd5Hash(md5Hash, password + userSalt);
                        }

                        var isadmin =
                            db.User.Where(x => x.UserName == name && x.Password == hashedPassword).Select(x => x.IsAdmin).SingleOrDefault();
                        if (isadmin == true)
                        {
                            var identity = new GenericIdentity(name);
                            SetPrincipal(new GenericPrincipal(identity, new[] { "Admin", "User" }));
                        }
                        else
                        {
                            var identity = new GenericIdentity(name);
                            SetPrincipal(new GenericPrincipal(identity, new[] { "User" }));
                        }
                    }

                }
                else
                {
                    // Invalid username or password.
                    HttpContext.Current.Response.StatusCode = 401;
                }
            }
            catch (FormatException)
            {
                // Credentials were not formatted correctly.
                HttpContext.Current.Response.StatusCode = 401;
            }
        }

        private static void OnApplicationAuthenticateRequest(object sender, EventArgs e)
        {
            var request = HttpContext.Current.Request;
            var authHeader = request.Headers["Authorization"];
            if (authHeader != null)
            {
                var authHeaderVal = AuthenticationHeaderValue.Parse(authHeader);

                // RFC 2617 sec 1.2, "scheme" name is case-insensitive
                if (authHeaderVal.Scheme.Equals("basic",
                        StringComparison.OrdinalIgnoreCase) &&
                    authHeaderVal.Parameter != null)
                {
                    AuthenticateUser(authHeaderVal.Parameter);
                }
            }
        }

        // If the request was unauthorized, add the WWW-Authenticate header 
        // to the response.
        private static void OnApplicationEndRequest(object sender, EventArgs e)
        {
            var response = HttpContext.Current.Response;
            if (response.StatusCode == 401)
            {
                response.Headers.Add("WWW-Authenticate",
                    string.Format("Basic realm=\"{0}\"", Realm));
            }
        }

        public void Dispose()
        {
        }
    }
}