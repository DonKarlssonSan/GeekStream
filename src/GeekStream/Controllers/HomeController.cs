﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using GeekStream.Core.Commands;
using GeekStream.Core.Queries;
using GeekStream.Models;

namespace GeekStream.Controllers
{
    public class HomeController : Controller
    {
        //
        // GET: /Home/
        public ActionResult Index()
        {
            return View();
        }
        //
        // GET: /Home/About/5
        public ActionResult About()
        {
            return View();
        }

		public ActionResult Feed(int id, int pageIndex = 0, bool popular = false)
		{
			var before = DateTime.Now;
			var model = ModelFactory.FeedModel(id, pageIndex, popular);
			ViewBag.QueryTime = DateTime.Now - before;

			if (Request.IsAjaxRequest())
				return PartialView(model);
			return View(model);
		}

		public ActionResult Page(long id)
		{
			// Todo : Send query to this action(add parameter) if click is from search.
			var query = new GetFeedItemById(id);
			var command = new RegisterClickCommand(id, string.Empty);
			MvcApplication.LiveDbClient.Execute(command);
			var model = MvcApplication.LiveDbClient.Execute(query);
			return Redirect(model.Url);
		}

		public ActionResult Search(string query, int pageIndex = 0, bool popular = false)
		{
			var before = DateTime.Now;
			var model = ModelFactory.SearchResultModel(query, pageIndex, popular);
			ViewBag.QueryTime = DateTime.Now - before;

			if (Request.IsAjaxRequest())
				return PartialView(model);

			return View(model);
		}

        [ChildActionOnly]
        public ActionResult Footer()
        {
            return PartialView("_footer",ModelFactory.IndexModel());
        }
    }
}
