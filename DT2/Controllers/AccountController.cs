using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Configuration;
using System.Web.Mvc;
using System.Web.Security;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using Microsoft.Owin.Security;
using DT2.Models;
using log4net;
using System.DirectoryServices.AccountManagement;

namespace DT2.Controllers
{
    [Authorize]
    public class AccountController : Controller
    {
        private static ILog logger = LogManager.GetLogger(typeof(AccountController));

        //
        // GET: /Account/Login
        [AllowAnonymous]
        public ActionResult Login(string returnUrl)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        //
        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult Login(LoginViewModel model, string returnUrl)
        {
            using (log4net.NDC.Push(Guid.NewGuid().ToString()))
            {

                if (ModelState.IsValid)
                {
                    try
                    {
                        bool valid = false;

                        logger.Debug("Validate user " + model.UserName + " on domain " + model.DomainName + " using this user's credentials");
                        using (PrincipalContext context = new PrincipalContext(ContextType.Domain, model.DomainName, model.UserName, model.Password))
                        {
                            logger.Debug("Validate credentials call using server " + context.ConnectedServer);
                            valid = context.ValidateCredentials(model.UserNameNoDomain, model.Password);
                        }


//                    if (Membership.ValidateUser(model.UserNameNoDomain, model.Password))
                        if (valid)
                        {
                            //FormsAuthentication.SetAuthCookie(model.UserNameNoDomain, model.RememberMe);
                            /// persist login details in the cookie
                            /// example from http://stackoverflow.com/questions/12804625/check-if-active-directory-password-is-different-from-cookie
                            var userData = LoginViewModel.JsonSerialize(model);
                            FormsAuthenticationTicket ticket = new FormsAuthenticationTicket(1,
                                model.UserName,
                                DateTime.Now,
                                DateTime.Now.AddMinutes(60*23), // force login on daily rather than hourly basis
                                model.RememberMe,
                                userData,
                                FormsAuthentication.FormsCookiePath);

                            // Encrypt the ticket.
                            string encTicket = FormsAuthentication.Encrypt(ticket);

                            // Create the cookie.
                            Response.Cookies.Add(new HttpCookie(FormsAuthentication.FormsCookieName, encTicket));

                            if (Url.IsLocalUrl(returnUrl) && returnUrl.Length > 1 && returnUrl.StartsWith("/")
                                && !returnUrl.StartsWith("//") && !returnUrl.StartsWith("/\\"))
                            {
                                return Redirect(returnUrl);
                            }
                            else
                            {
                                return RedirectToAction("Index", "Services");
                            }
                        }
                        else
                        {
                            ModelState.AddModelError("", "Invalid username or password.");
                        }
                    }
                    catch (System.Configuration.ConfigurationErrorsException exCfg)
                    {
                        logger.Error("Deployed with bad config.  Might be AD controller name.", exCfg);
                    }
                    catch (SystemException ex)
                    {
                        logger.Error("Problem validating user credentials.", ex);
                    }
                }
                // If we got this far, something failed, redisplay form
                return View(model);
            }
        }

        //
        // POST: /Account/LogOff
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult LogOff()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login", "Account");
        }
    }
}