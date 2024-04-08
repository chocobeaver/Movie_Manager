﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using MoviesDBManager.Models;
using Mail;
using System.Deployment.Internal;


namespace MoviesDBManager.Controllers
{
    public class AccountsController : Controller
    {

        public ActionResult UsersList()
        {
            Session["LastAction"] = "/Accounts/UsersList";
            return View();
        }
        public ActionResult GroupEmails()
        {
            Session["LastAction"] = "/Accounts/GroupEmails";
            return View();
        }
        public ActionResult Users(bool forceRefresh = false)
        {
            if (forceRefresh || DB.Users.HasChanged)
            {
                return PartialView(DB.Users.ToList().OrderBy(c => c.GetFullName()));
            }
            return null;
        }

       


        #region Account creation
        [HttpPost]
        public JsonResult EmailAvailable(string email)
        {
            User onLineUser = OnlineUsers.GetSessionUser();
            int id = onLineUser != null? onLineUser.Id : 0;
            return Json(DB.Users.EmailAvailable(email, id));
        }
        [HttpPost]
        public JsonResult EmailExist(string email)
        {
            return Json(DB.Users.EmailExist(email));
        }

        public ActionResult Subscribe()
        {
            ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
            User user = new User();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken()]
        public ActionResult Subscribe(User user)
        {
            user.UserTypeId = 3; // self subscribed user 
            if (ModelState.IsValid)
            {
                if (user.Avatar == Models.User.DefaultImage) 
                {
                    // required avatar image
                    ModelState.AddModelError("Avatar", "Veuillez choisir votre avatar");
                    ViewBag.Genders = SelectListUtilities<Gender>.Convert(DB.Genders.ToList());
                }
                else
                {
                    user = DB.Users.Create(user);
                    if (user != null)
                    {
                        SendEmailVerification(user, user.Email);
                        return RedirectToAction("SubscribeDone/" + user.Id.ToString());
                    }
                    else
                        return RedirectToAction("Report", "Errors", new { message = "Échec de création de compte" });
                }
            }
            return View(user);
        }
        [OnlineUsers.AdminAccess]
        public ActionResult BlockUser(int id )
        {
            User User1 = DB.Users.Get(id);
            User1.Blocked= User1.Blocked ? false : true;

            string subject= User1.Blocked ? "block" : "unblock";
            string body = User1.Blocked ? $"{User1.GetFullName()} vous avez ete blocker" : $"{User1.GetFullName()} vous avez ete deblocker";
            SMTP.SendEmail(User1.GetFullName(), User1.Email, subject, body);
            DB.Users.Update(User1);
            return RedirectToAction("UsersList");
        }



    public ActionResult promoteUser(int id)
        {
            User User1 = DB.Users.Get(id);
            if (User1.UserTypeId == 3)
                User1.UserTypeId = 2;
            else if (User1.UserTypeId == 2)
                User1.UserTypeId = 1;
            else if (User1.UserTypeId == 1)
                User1.UserTypeId = 3;
            return RedirectToAction("UsersList");
        }
        public ActionResult SubscribeDone(int id)
        {
            User newlySubscribedUser = DB.Users.Get(id);
            if (newlySubscribedUser != null)
                return View(newlySubscribedUser);
            return RedirectToAction("Login");
        }
        [OnlineUsers.AdminAccess]
        public ActionResult Delete(int id)
        {
            User user = DB.Users.Get(id);
            if (user != null)
            {
                SMTP.SendEmail(user.FirstName, user.Email, "Compte supprimé", "Votre compte à été supprimé ");
                DB.Users.Delete(id);
            }
            return RedirectToAction("UsersList");
        }
        #endregion

        #region Account Verification
        public void SendEmailVerification(User user, string newEmail)
        {
            if (user.Id != 0)
            {
                UnverifiedEmail unverifiedEmail = DB.Users.Add_UnverifiedEmail(user.Id, newEmail);
                if (unverifiedEmail != null)
                {
                    string verificationUrl = Url.Action("VerifyUser", "Accounts", null, Request.Url.Scheme);
                    String Link = @"<br/><a href='" + verificationUrl + "?code=" + unverifiedEmail.VerificationCode + @"' > Confirmez votre inscription...</a>";

                    String suffixe = "";
                    if (user.GenderId == 2)
                    {
                        suffixe = "e";
                    }
                    string Subject = "Movies Database - Vérification d'inscription...";

                    string Body = "Bonjour " + user.GetFullName(true) + @",<br/><br/>";
                    Body += @"Merci de vous être inscrit" + suffixe + " au site ChatManager. <br/>";
                    Body += @"Pour utiliser votre compte vous devez confirmer votre inscription en cliquant sur le lien suivant : <br/>";
                    Body += Link;
                    Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                    Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='mailto:"
                         + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site ChatManager)";

                    SMTP.SendEmail(user.GetFullName(), unverifiedEmail.Email, Subject, Body);
                }
            }
        }
        public ActionResult VerifyDone(int id)
        {
            User newlySubscribedUser = DB.Users.Get(id);
            if (newlySubscribedUser != null)
                return View(newlySubscribedUser);
            return RedirectToAction("Login");
        }
        public ActionResult VerifyError()
        {
            return View();
        }
        public ActionResult AlreadyVerified()
        {
            return View();
        }
        public ActionResult VerifyUser(string code)
        {
            UnverifiedEmail UnverifiedEmail = DB.Users.FindUnverifiedEmail(code);
            if (UnverifiedEmail != null)
            {
                User newlySubscribedUser = DB.Users.Get(UnverifiedEmail.UserId);

                if (newlySubscribedUser != null)
                {
                    if (DB.Users.EmailVerified(newlySubscribedUser.Email))
                        return RedirectToAction("AlreadyVerified");

                    if (DB.Users.Verify_User(newlySubscribedUser.Id, code))
                        return RedirectToAction("VerifyDone/" + newlySubscribedUser.Id);
                }
                else
                    RedirectToAction("VerifyError");
            }
            return RedirectToAction("VerifyError");
        }
        #endregion

        #region EmailChange
        public ActionResult EmailChangedAlert()
        {
            OnlineUsers.RemoveSessionUser();
            return View();
        }
        public ActionResult CommitEmailChange(string code)
        {
            if (DB.Users.ChangeEmail(code))
            {
                return RedirectToAction("EmailChanged");
            }
            return RedirectToAction("EmailChangedError");
        }
        public ActionResult EmailChanged()
        {
            return View();
        }
        public ActionResult EmailChangedError()
        {
            return View();
        }
        public void SendEmailChangedVerification(User user, string newEmail)
        {
            if (user.Id != 0)
            {
                UnverifiedEmail unverifiedEmail = DB.Users.Add_UnverifiedEmail(user.Id, newEmail);
                if (unverifiedEmail != null)
                {
                    string verificationUrl = Url.Action("CommitEmailChange", "Accounts", null, Request.Url.Scheme);
                    String Link = @"<br/><a href='" + verificationUrl + "?code=" + unverifiedEmail.VerificationCode + @"' > Confirmez votre adresse...</a>";

                    string Subject = "ChatManager - Confirmation de changement de courriel...";

                    string Body = "Bonjour " + user.GetFullName(true) + @",<br/><br/>";
                    Body += @"Vous avez modifié votre adresse de courriel. <br/>";
                    Body += @"Pour que ce changement soit pris en compte, vous devez confirmer cette adresse en cliquant sur le lien suivant : <br/>";
                    Body += Link;
                    Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                    Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='mailto:"
                         + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site ChatManager)";

                    SMTP.SendEmail(user.GetFullName(), unverifiedEmail.Email, Subject, Body);
                }
            }
        }
        #endregion

        #region ResetPassword
        public ActionResult ResetPasswordCommand()
        {
            return View();
        }
        [HttpPost]
        public ActionResult ResetPasswordCommand(string Email)
        {
            if (ModelState.IsValid)
            {
                SendResetPasswordCommandEmail(Email);
                return RedirectToAction("ResetPasswordCommandAlert");
            }
            return View(Email);
        }
        public void SendResetPasswordCommandEmail(string email)
        {
            ResetPasswordCommand resetPasswordCommand = DB.Users.Add_ResetPasswordCommand(email);
            if (resetPasswordCommand != null)
            {
                User user = DB.Users.Get(resetPasswordCommand.UserId);
                string verificationUrl = Url.Action("ResetPassword", "Accounts", null, Request.Url.Scheme);
                String Link = @"<br/><a href='" + verificationUrl + "?code=" + resetPasswordCommand.VerificationCode + @"' > Réinitialisation de mot de passe...</a>";

                string Subject = "Répertoire de films - Réinitialisaton ...";

                string Body = "Bonjour " + user.GetFullName(true) + @",<br/><br/>";
                Body += @"Vous avez demandé de réinitialiser votre mot de passe. <br/>";
                Body += @"Procédez en cliquant sur le lien suivant : <br/>";
                Body += Link;
                Body += @"<br/><br/>Ce courriel a été généré automatiquement, veuillez ne pas y répondre.";
                Body += @"<br/><br/>Si vous éprouvez des difficultés ou s'il s'agit d'une erreur, veuillez le signaler à <a href='mailto:"
                     + SMTP.OwnerEmail + "'>" + SMTP.OwnerName + "</a> (Webmestre du site [nom de l'application])";

                SMTP.SendEmail(user.GetFullName(), user.Email, Subject, Body);
            }
        }
        public ActionResult ResetPassword(string code)
        {
            ResetPasswordCommand resetPasswordCommand = DB.Users.Find_ResetPasswordCommand(code);
            if (resetPasswordCommand != null)
                return View(new PasswordView() { Code = code });
            return RedirectToAction("ResetPasswordError");
        }
        [HttpPost]
        [ValidateAntiForgeryToken()]
        public ActionResult ResetPassword(PasswordView passwordView)
        {
            if (ModelState.IsValid)
            {
                ResetPasswordCommand resetPasswordCommand = DB.Users.Find_ResetPasswordCommand(passwordView.Code);
                if (resetPasswordCommand != null)
                {
                    if (DB.Users.ResetPassword(resetPasswordCommand.UserId, passwordView.Password))
                        return RedirectToAction("ResetPasswordSuccess");
                    else
                        return RedirectToAction("ResetPasswordError");
                }
                else
                    return RedirectToAction("ResetPasswordError");
            }
            return View(passwordView);
        }
        public ActionResult ResetPasswordCommandAlert()
        {
            return View();
        }
        public ActionResult ResetPasswordSuccess()
        {
            return View();
        }
        public ActionResult ResetPasswordError()
        {
            return View();
        }
        #endregion

        #region Profil
        [OnlineUsers.UserAccess]
        public ActionResult Profil()
        {
            User userToEdit = OnlineUsers.GetSessionUser().Clone();
            if (userToEdit != null)
            {
                ViewBag.Genders = new SelectList(DB.Genders.ToList(), "Id", "Name", userToEdit.GenderId);
                Session["UnchangedPasswordCode"] = Guid.NewGuid().ToString().Substring(0, 12);
                userToEdit.Password = userToEdit.ConfirmPassword = (string)Session["UnchangedPasswordCode"];
                return View(userToEdit);
            }
            return null;
        }

        [HttpPost]
        [ValidateAntiForgeryToken()]
        public ActionResult Profil(User user)
        {
            User currentUser = OnlineUsers.GetSessionUser();
            user.Id = currentUser.Id;
            user.Verified = currentUser.Verified;
            user.UserTypeId = currentUser.UserTypeId;
            user.Blocked = currentUser.Blocked;
            user.CreationDate = currentUser.CreationDate;

            string newEmail = "";
            if (ModelState.IsValid)
            {
                if (user.Password == (string)Session["UnchangedPasswordCode"])
                    user.Password = user.ConfirmPassword = currentUser.Password;

                if (user.Email != currentUser.Email)
                {
                    newEmail = user.Email;
                    user.Email = user.ConfirmEmail = currentUser.Email;
                }

                if (DB.Users.Update(user))
                {
                    if (newEmail != "")
                    {
                        SendEmailChangedVerification(user, newEmail);
                        return RedirectToAction("EmailChangedAlert");
                    }
                    else
                        return Redirect((string)Session["LastAction"]);
                }
            }
            ViewBag.Genders = new SelectList(DB.Genders.ToList(), "Id", "Name", user.GenderId);
            return View(currentUser);
        }
        #endregion

        #region Login and Logout
        public ActionResult Login(string message)
        {
            ViewBag.Message = message;
            return View(new LoginCredential());

        }
        [HttpPost]
        [ValidateAntiForgeryToken()]
        public ActionResult Login(LoginCredential loginCredential)
        {
            if (ModelState.IsValid)
            {
                if (DB.Users.EmailBlocked(loginCredential.Email))
                {
                    ModelState.AddModelError("Email", "Ce compte est bloqué.");
                    return View(loginCredential);
                }
                if (!DB.Users.EmailVerified(loginCredential.Email))
                {
                    ModelState.AddModelError("Email", "Ce courriel n'est pas vérifié.");
                    return View(loginCredential);
                }
                User user = DB.Users.GetUser(loginCredential);
               
                if (user == null)
                {
                    ModelState.AddModelError("Password", "Mot de passe incorrect.");
                    return View(loginCredential);
                }
                if(!OnlineUsers.IsOnline(user.Id)) { OnlineUsers.ConnectedUsersId.Add(user.Id); }
                OnlineUsers.AddSessionUser(user.Id);
                return RedirectToAction("Index", "Movies");
            }

            return View(loginCredential);
        }
        public ActionResult Logout()
        {
            User loggedUser = OnlineUsers.GetSessionUser();
            if (loggedUser != null)
            {
                if (OnlineUsers.IsOnline(loggedUser.Id)) { OnlineUsers.ConnectedUsersId.Remove(loggedUser.Id); }
                OnlineUsers.RemoveSessionUser();
            }
            return RedirectToAction("Login");
        }
        
        #endregion
    }
}
