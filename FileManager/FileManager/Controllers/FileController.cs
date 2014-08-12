using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using System.Web.Mvc;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure;
using System.Configuration;

namespace FileManager.Controllers
{
	public class FileController : Controller
	{
		
		private const string defaultContainerName = "New folder";
		
		public ActionResult Upload(HttpPostedFileBase file)
		{
			return Json(new
			{
				Success = true,
				FileName = file.FileName,
				FileSize = file.ContentLength
			}, JsonRequestBehavior.AllowGet);
		}

		public ActionResult Index()
		{
			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);

			var containers = blobClient.ListContainers();

			return View(containers);
		}

		public JsonResult GetContainerBlobs(string containerName, string prefix = null)
		{
			if (string.IsNullOrWhiteSpace(containerName))
			{
				throw new ArgumentNullException("containerName", "Cannot get files without container");
			}

			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);
			CloudBlobContainer container = GetContainer(blobClient, containerName);

			GetContainerBlobsViewModel vm = new GetContainerBlobsViewModel()
			{
				CurrentContainerName = containerName
			};

			var permissions = container.GetPermissions();
			if (permissions.PublicAccess == BlobContainerPublicAccessType.Off)
			{
				vm.IsPrivateContainer = true;
			}
			else
			{
				vm.IsPrivateContainer = false;
			}
			//ViewBag.ContainerName = containerName;
			//ViewBag.Prefix = prefix;

			vm.Blobs.AddRange(container.ListBlobs(prefix, false));

			// Loop over items within the container and output the length and URI.
			foreach (IListBlobItem item in vm.Blobs)
			{
				if (item.GetType() == typeof(CloudBlockBlob))
				{
					CloudBlockBlob blob = (CloudBlockBlob)item;
					Console.WriteLine("Block blob of length {0}: {1}", blob.Properties.Length, blob.Uri);
				}
				else if (item.GetType() == typeof(CloudPageBlob))
				{
					CloudPageBlob pageBlob = (CloudPageBlob)item;
					Console.WriteLine("Page blob of length {0}: {1}", pageBlob.Properties.Length, pageBlob.Uri);
				}
				else if (item.GetType() == typeof(CloudBlobDirectory))
				{
					CloudBlobDirectory directory = (CloudBlobDirectory)item;
					Console.WriteLine("Directory: {0}", directory.Uri);
				}
			}

			return Json(vm, JsonRequestBehavior.AllowGet);
		}

		public ActionResult CreateContainer()
		{
			var vm = new CreateContainerViewModel()
			{
				Name = defaultContainerName
			};

			return View(vm);
		}

		[HttpPost]
		public ActionResult CreateContainer(CreateContainerViewModel vm)
		{
			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);
			CloudBlobContainer container = GetContainer(blobClient, vm.Name);

			// Create the container if it doesn't already exist.
			container.CreateIfNotExists();

			// Set the container as public.
			container.SetPermissions(
				new BlobContainerPermissions
				{
					PublicAccess = BlobContainerPublicAccessType.Blob
				});

			return RedirectToAction("Index");
		}

		public ActionResult UploadFile(string containerName, string prefix = null)
		{
			if (string.IsNullOrWhiteSpace(containerName))
			{
				throw new ArgumentNullException("containerName", "Cannot upload file without container");
			}

			ViewBag.ContainerName = containerName;
			ViewBag.Prefix = prefix;

			return View();
		}

		[HttpPost]
		public ActionResult UploadFile(string containerName, HttpPostedFileWrapper file, string prefix = null, string subdirectory = null)
		{
			if (string.IsNullOrWhiteSpace(containerName))
			{
				throw new ArgumentNullException("containerName", "Cannot upload file without container");
			}

			if (file == null)
			{
				throw new ArgumentNullException("file", "No file uploaded");
			}

			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);
			CloudBlobContainer container = GetContainer(blobClient, containerName);

			string fileName = Path.GetFileNameWithoutExtension(file.FileName);
			if (!ValidateFileName(fileName))
			{
				throw new ArgumentException("Incorrect filename", fileName);
			}
			string fileNameSaved = fileName.ToLower();
			if (!string.IsNullOrWhiteSpace(subdirectory))
			{
				subdirectory = subdirectory.Trim().ToLower() + "/";
			}
			fileNameSaved = prefix + subdirectory + fileNameSaved + Path.GetExtension(file.FileName);

			// Retrieve reference to a blob named "fotoRalph".
			CloudBlockBlob blockBlob = container.GetBlockBlobReference(fileNameSaved);

			// Create or overwrite the "fotoRalph" blob with contents from a local file.
			blockBlob.UploadFromStream(file.InputStream);

			return RedirectToAction("ContainerBlobs", new { containerName = containerName, prefix = prefix });
		}

		public FileResult GetFile(string containerName, string fileName, string prefix = null)
		{
			if (string.IsNullOrWhiteSpace(containerName))
			{
				throw new ArgumentNullException("containerName", "Cannot get file without container");
			}

			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);
			CloudBlobContainer container = GetContainer(blobClient, containerName);

			// Retrieve reference to a blob named "photo1.jpg".
			CloudBlockBlob blockBlob = container.GetBlockBlobReference(prefix + fileName);

			MemoryStream stream = new MemoryStream();

			try
			{
				blockBlob.DownloadToStream(stream);
				var bytes = stream.ToArray();

				return File(bytes, Path.GetExtension(blockBlob.Name), blockBlob.Name);
			}
			finally
			{
				stream.Close();
			}
		}

		[HttpPost]
		public ActionResult DeleteFile(string containerName, string fileName, string prefix = null)
		{
			if (string.IsNullOrWhiteSpace(containerName))
			{
				throw new ArgumentNullException("containerName", "Cannot delete file without container");
			}

			if (string.IsNullOrWhiteSpace(fileName))
			{
				throw new ArgumentNullException("fileName", "Cannot delete file without storage uri");
			}

			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);
			CloudBlobContainer container = GetContainer(blobClient, containerName);

			// Retrieve reference to a blob named "myblob.txt".
			var blob = container.GetBlobReferenceFromServer(fileName);

			// Delete the blob.
			blob.Delete();

			return RedirectToAction("ContainerBlobs", new { containerName = containerName, prefix = prefix });
		}

		#region Private Methods

		private static CloudBlobClient GetBlobClient(CloudStorageAccount storageAccount)
		{
			// Create the blob client.
			CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
			return blobClient;
		}

		private static CloudBlobContainer GetContainer(CloudBlobClient blobClient, string containerName)
		{
			// Retrieve a reference to a container.
			CloudBlobContainer container = blobClient.GetContainerReference(containerName);
			return container;
		}

		private static CloudStorageAccount GetStorageAccount()
		{
			// Retrieve storage account from connection string.
			CloudStorageAccount storageAccount = CloudStorageAccount.Parse(ConfigurationManager.ConnectionStrings["StorageConnectionString"].ConnectionString);
			return storageAccount;
		}

		private static bool ValidateFileName(string fileName)
		{
			Regex fileNameRegex = new Regex("[a-zA-Z0-9]+");
			return fileNameRegex.IsMatch(fileName);
		}

		#endregion

	}

	public class GetContainerBlobsViewModel
	{

		private readonly List<IListBlobItem> blobs = new List<IListBlobItem>();

		public List<IListBlobItem> Blobs
		{
			get
			{
				return blobs;
			}
		}

		public int CountBlobs
		{
			get
			{
				return blobs.Count;
			}
		}

		public string CurrentContainerName { get; set; }

		public bool IsPrivateContainer { get; set; }

	}

	public class CreateContainerViewModel
	{
		[Required]
		[RegularExpression("[a-zA-Z0-9]+")]
		public string Name { get; set; }
	}
}