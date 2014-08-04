﻿using System.Runtime.Remoting;
using System.Web.Mvc;
using BrickPile.Core;
using BrickPile.Samples.Models;
using BrickPile.UI.Web.ViewModels;
using Raven.Client;

namespace BrickPile.Samples.Controllers
{
    [AllowAnonymous]
    public class ContainerController : Controller
    {
        private readonly IDocumentStore store;
        //
        // GET: /Container/

        public ActionResult Index(Container currentPage) {            
            using (var session = this.store.OpenSession())
            {
                var navigationContext = new NavigationContext(ControllerContext.RequestContext);
                var viewModel = new DefaultViewModel<Container>
                {
                    CurrentPage = currentPage,
                    NavigationContext = navigationContext
                };

                return View(viewModel);
            }
        }

        public ContainerController(IDocumentStore store) {
            this.store = store;
        }
    }
}
