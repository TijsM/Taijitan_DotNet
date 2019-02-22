﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Taijitan.Models.Domain;
using Taijitan.Models.UserViewModel;

namespace Taijitan.Controllers
{
    [Authorize]
    public class UserController : Controller
    {
        private readonly IUserRepository _userRepository;
        private readonly ICityRepository _cityRepository;
        private readonly UserManager<IdentityUser> _userManager;

        public UserController(IUserRepository userRepository,ICityRepository cityRepository, UserManager<IdentityUser> userManager)
        {
            _userRepository = userRepository;
            _cityRepository = cityRepository;
            _userManager = userManager;
        }  
        public IActionResult Edit()
        {
            string userEmail = _userManager.GetUserName(HttpContext.User);
            User u = _userRepository.GetByEmail(userEmail);
            if (u == null)
                return NotFound();

            ViewData["userId"] = u.UserId;
            return View(new EditViewModel(u));
        }
        [HttpPost]
        public IActionResult Edit(int id,EditViewModel evm)
        {
            User u = null;
            if (ModelState.IsValid)
            {
                
                try
                {
                    u = _userRepository.GetById(id);
                    u.Change(evm.Name, evm.FirstName, evm.DateOfBirth, evm.Street, _cityRepository.GetByPostalCode(evm.PostalCode), evm.Country, evm.HouseNumber, evm.PhoneNumber, evm.Email);
                    _userRepository.SaveChanges();
                }
                catch
                {

                }
            }
            return RedirectToAction("Index","Home");
        }
    }
}