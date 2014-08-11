using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace FileManager.Controllers
{
	public class FileController : Controller
	{
		public ActionResult Upload(HttpPostedFileBase file)
		{
			return Json(new
			{
				Success = true,
				FileName = file.FileName,
				FileSize = file.ContentLength
			}, JsonRequestBehavior.AllowGet);
		}
	}
}