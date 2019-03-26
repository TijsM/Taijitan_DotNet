﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using MimeKit;
using Newtonsoft.Json;
using Taijitan.Models.Domain;
using Taijitan.Models.ViewModels;
using MailKit.Net.Smtp;
using Taijitan.Filters;
using Microsoft.AspNetCore.Authorization;

namespace Taijitan.Controllers
{
    [ServiceFilter(typeof(SessionFilter))]
    [ServiceFilter(typeof(CourseMaterialFilter))]
    [ServiceFilter(typeof(HomeFilter))]
    [Authorize]
    public class CourseMaterialController : Controller
    {
        private readonly ISessionRepository _sessionRepository;
        private readonly IUserRepository _userRepository;
        private readonly ICourseMaterialRepository _courseMaterialRepository;
        private readonly ICommentRepository _commentRepository;

        public CourseMaterialController(ISessionRepository sessionRepository, IUserRepository userRepository,
            ICourseMaterialRepository courseMaterialRepository, ICommentRepository commentRepository)
        {
            _sessionRepository = sessionRepository;
            _userRepository = userRepository;
            _courseMaterialRepository = courseMaterialRepository;
            _commentRepository = commentRepository;
        }
        public IActionResult Confirm(int id, Session sessionFilter)
        {
            sessionFilter = _sessionRepository.GetById(id);
            if (sessionFilter == null)
                return NotFound();

            sessionFilter.Start();
            _sessionRepository.SaveChanges();

            ViewData["partialView"] = "";
            CourseMaterialViewModel vm = new CourseMaterialViewModel()
            {
                Session = sessionFilter,
                CourseMaterials = _courseMaterialRepository.GetByRank(Rank.Kyu6),
                AllRanks = GiveAllRanksAsList(),
                SelectedRank = Rank.Kyu6
            };
            return View("Training", vm);
        }
        [HttpPost]
        public IActionResult SelectMember(int sessionId, int id)
        {
            ViewData["partialView"] = "lessons";
            CourseMaterialViewModel vm = new CourseMaterialViewModel()
            {
                Session = _sessionRepository.GetById(sessionId),
                CourseMaterials = _courseMaterialRepository.GetByRank(Rank.Kyu6),
                AllRanks = GiveAllRanksAsList(),
                SelectedMember = (Member)_userRepository.GetById(id),
                SelectedRank = Rank.Kyu6
            };
            return View("Training", vm);
        }
        private ICollection<Rank> GiveAllRanksAsList()
        {
            return Enum.GetValues(typeof(Rank)).Cast<Rank>().ToList();
        }
        public IActionResult SelectRank(int sessionId, Rank rank, int selectedUserId)
        {
            ViewData["partialView"] = "lessons";
            CourseMaterialViewModel vm = new CourseMaterialViewModel()
            {
                Session = _sessionRepository.GetById(sessionId),
                CourseMaterials = _courseMaterialRepository.GetByRank(rank),
                AllRanks = GiveAllRanksAsList(),
                SelectedMember = (Member)_userRepository.GetById(selectedUserId),
                SelectedRank = rank
            };
            return View("Training", vm);
        }
        public IActionResult SelectCourse(int sessionId, Rank rank, int selectedUserId, int matId, CourseMaterialViewModel cmvm)
        {
            Session session = _sessionRepository.GetById(sessionId);
            ViewData["partialView"] = "course";
            cmvm = new CourseMaterialViewModel()
            {
                Session = session,
                CourseMaterials = _courseMaterialRepository.GetByRank(rank),
                SelectedCourseMaterial = _courseMaterialRepository.GetById(matId),
                AllRanks = GiveAllRanksAsList(),
                SelectedMember = session.MembersPresent.SingleOrDefault(m => m.UserId == selectedUserId),
                SelectedRank = rank
            };
            //viewModel in session steken
            return View("Training", cmvm);
        }

        [HttpPost]
        public IActionResult AddComment(string comment, CourseMaterialViewModel cmvm, ICollection<Comment> notifications)
        {
            if (cmvm != null)
            {
                //viewModel uit session halen
                CourseMaterial course = _courseMaterialRepository.GetById(cmvm.SelectedCourseMaterial.MaterialId);
                Member member = (Member)_userRepository.GetById(cmvm.SelectedMember.UserId);
                Comment c = new Comment(comment, course, member);
                _commentRepository.Add(c);
                _commentRepository.SaveChanges();
                SendMail(c);
                TempData["message"] = "Het commentaar is succesvol verstuurd!";
                
                //Notificaties
                if (notifications != null)
                {
                    notifications.OrderBy(n => n.DateCreated);
                    while(notifications.Where(n => n.IsRead).Count() > 0 && notifications.Count() > 5)
                    {
                        notifications.Remove(notifications.Last());
                    }
                    notifications.Add(c);
                } else
                {
                    notifications = new List<Comment>();
                    notifications.Add(c);
                }
                return RedirectToAction(nameof(SelectCourse), new { sessionId = cmvm.Session.SessionId, rank = cmvm.SelectedRank,
                    selectedUserId = cmvm.SelectedMember.UserId, matId = cmvm.SelectedCourseMaterial.MaterialId });
            }
            return View("Training");
        }
        [HttpGet]
        public IActionResult ViewComments()
        {
            ViewData["IsEmpty"] = true;
            return ShowComments();
        }
        public IActionResult SelectComment(int id)
        {
            Comment comment = _commentRepository.GetById(id);

            if (comment == null)
            {
                ViewData["IsEmpty"] = true;
            }
            else
            {
                ViewData["IsEmpty"] = false;
                ViewData["Comment"] = comment;

            }
            return ShowComments();
        }

        public IActionResult RemoveComment(int id)
        {
            Comment comment = _commentRepository.GetById(id);
            if (comment == null)
                return NotFound();

            _commentRepository.Delete(comment);
            _commentRepository.SaveChanges();
            ViewData["IsEmpty"] = true;
            return ShowComments();
        }

        private IActionResult ShowComments()
        {
            var comments = _commentRepository.GetAll();
            return View("ViewComments", comments);
        }

        private void SendMail(Comment comment)
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress("project.groep08@gmail.com"));
            message.To.Add(new MailboxAddress("receivetaijitan@maildrop.cc"));
            message.Subject = "nieuwe commentaar";
            message.Body = new TextPart("html")
            {
                Text =
                "Gebruiker die commentaar leverde: " + comment.Member.FirstName +" " + comment.Member.Name
                + "<br />"
                + "Datum van de commentaar: " + comment.DateCreated.ToShortDateString()
                + "<br />"
                + "Lesmateriaal van de commentaar: " + comment.Course.Title
                + "<br />"
                + "Commentaar: "
                + "<br />"
                + comment.Content
            };

            using (var client = new SmtpClient())
            {
                client.Connect("smtp.gmail.com", 587);
                client.Authenticate("groep08.project@gmail.com", "_123Groep8_123_");

                client.Send(message);
                client.Disconnect(true);
            }
        }
    }
}