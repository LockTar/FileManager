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

		public enum BlobItemType
		{
			CloudBlockBlob = 0,
			CloudBlobDirectory = 1
		}

		private const string defaultContainerName = "New folder";

		public ActionResult Upload(HttpPostedFileBase file, string containerName, string prefix = null)
		{
			string subdirectory = "";

			CheckContainerName(containerName, "Cannot upload file without container");
			CheckUploadedFile(file, "No file uploaded");

			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);
			CloudBlobContainer container = GetContainer(blobClient, containerName);

			string blobName = CreateBlobName(file.FileName, prefix, ref subdirectory);

			// Retrieve reference to a blob named "fotoRalph".
			CloudBlockBlob blockBlob = container.GetBlockBlobReference(blobName);

			// Create or overwrite the "fotoRalph" blob with contents from a local file.
			blockBlob.UploadFromStream(file.InputStream);

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
			CheckContainerName(containerName, "Cannot get files without container");

			CloudStorageAccount storageAccount = GetStorageAccount();
			CloudBlobClient blobClient = GetBlobClient(storageAccount);
			CloudBlobContainer container = GetContainer(blobClient, containerName);

			ContainerViewModel vm = new ContainerViewModel()
			{
				ContainerName = containerName,
				PrefixFull = prefix,
				PrefixParentFull = GetPrefixFullParent(prefix),
				IsRootDirectory = string.IsNullOrWhiteSpace(prefix)
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

			var allBlobs = container.ListBlobs(prefix, false);
			var blobs = new List<BlobItemViewModel>();

			// Loop over items within the container and output the length and URI.
			foreach (IListBlobItem item in allBlobs)
			{
				if (item.GetType() == typeof(CloudBlockBlob))
				{
					CloudBlockBlob blob = (CloudBlockBlob)item;
					Console.WriteLine("Block blob of length {0}: {1}", blob.Properties.Length, blob.Uri);

					// Get the name and prefix of the blob
					string blobName = GetBlobName(blob.Name);
					string prefixFull = GetPrefixFull(blob.Name);
					string prefixLast = GetPrefixLast(blob.Name);

					blobs.Add(new BlobItemViewModel()
					{
						BlobType = BlobItemType.CloudBlockBlob,
						Name = blobName,
						Uri = blob.Uri.AbsoluteUri,
						PrefixFull = prefixFull,
						PrefixLast = prefixLast
					});
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

					var prefixSegments = directory.Prefix.Split(new char[] { '/' });
					string prefixLast;
					if (prefixSegments.Length > 1)
						prefixLast = prefixSegments[prefixSegments.Length - 1];
					else
						prefixLast = string.Empty;

					blobs.Add(new BlobItemViewModel()
					{
						BlobType = BlobItemType.CloudBlobDirectory,
						Name = directory.Uri.Segments.Last().Replace("/", ""),
						Uri = directory.Uri.AbsoluteUri,
						PrefixFull = directory.Prefix,
						PrefixLast = prefixLast
					});
				}
			}

			// Order the blobs and add them to the viewmodel
			blobs = blobs.OrderBy(b => b.IsDirectory == false).ToList();
			vm.Blobs.AddRange(blobs);

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
			CheckContainerName(containerName, "Cannot upload file without container");

			ViewBag.ContainerName = containerName;
			ViewBag.Prefix = prefix;

			return View();
		}

		[HttpPost]
		public ActionResult UploadFile(string containerName, HttpPostedFileWrapper file, string prefix = null, string subdirectory = null)
		{
			CheckContainerName(containerName, "Cannot upload file without container");
			CheckUploadedFile(file, "No file uploaded");

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
		public void DeleteFile(string containerName, string fileName, string prefix = null)
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
			var blob = container.GetBlobReferenceFromServer(prefix + fileName);

			// Delete the blob.
			blob.Delete();
		}

		#region Private Methods

		private static void CheckContainerName(string containerName, string errorMessage)
		{
			if (string.IsNullOrWhiteSpace(containerName))
			{
				throw new ArgumentNullException("containerName", errorMessage);
			}
		}

		private static void CheckUploadedFile(HttpPostedFileBase file, string errorMessage)
		{
			if (file == null)
			{
				throw new ArgumentNullException("file", errorMessage);
			}
		}

		////private static string CreateThumbBlobName(string uploadFileName, string prefix, ref string subdirectory)
		////{
		////	string blobName = CreateBlobNameWithoutExtension(uploadFileName, prefix, ref subdirectory);
		////	blobName = blobName + Path.GetExtension(uploadFileName);

		////	return blobName;
		////}

		private static string CreateBlobName(string uploadFileName, string prefix, ref string subdirectory)
		{
			string blobName = CreateBlobNameWithoutExtension(uploadFileName, prefix, ref subdirectory);
			blobName = blobName + Path.GetExtension(uploadFileName);

			return blobName;
		}

		private static string CreateBlobNameWithoutExtension(string uploadFileName, string prefix, ref string subdirectory)
		{
			string fileName = Path.GetFileNameWithoutExtension(uploadFileName);
			if (!ValidateFileName(fileName))
			{
				throw new ArgumentException("Incorrect filename", fileName);
			}
			string blobName = fileName.ToLower();
			if (!string.IsNullOrWhiteSpace(subdirectory))
			{
				subdirectory = subdirectory.Trim().ToLower() + "/";
			}

			blobName = prefix + subdirectory + blobName;

			return blobName;
		}

		private static CloudBlobClient GetBlobClient(CloudStorageAccount storageAccount)
		{
			// Create the blob client.
			CloudBlobClient blobClient = storageAccount.CreateCloudBlobClient();
			return blobClient;
		}


		private static string GetPrefixFull(string blobName)
		{
			var nameSegments = GetBlobNameSegments(blobName);
			string prefixFull = string.Empty;

			if (nameSegments.Length > 1)
			{
				// Get the full prefix
				for (int i = 0; i < nameSegments.Length - 1; i++)
				{
					if (string.IsNullOrWhiteSpace(prefixFull))
					{
						prefixFull = nameSegments[i] + "/";
					}
					else
					{
						prefixFull = string.Format("{0}{1}/", prefixFull, nameSegments[i]);
					}
				}
			}
			else
			{
				prefixFull = string.Empty;
			}

			return prefixFull;
		}

		private static string GetPrefixLast(string blobName)
		{
			var nameSegments = GetBlobNameSegments(blobName);
			string prefixLast = string.Empty;

			if (nameSegments.Length > 1)
			{
				// Get the prefix of the blob
				prefixLast = nameSegments[nameSegments.Length - 2];
			}
			else
			{
				prefixLast = string.Empty;
			}

			return prefixLast;
		}

		/// <summary>
		/// Get the full prefix of the parent directory.
		/// </summary>
		/// <param name="prefix">The current prefix of the current directory.</param>
		/// <returns>The full prefix of the parent directory otherwise empty string.</returns>
		public static string GetPrefixFullParent(string prefix)
		{
			var nameSegments = GetBlobNameSegments(prefix);
			string prefixParent = string.Empty;

			if (nameSegments.Length > 1)
			{
				// Get the full prefix
				for (int i = 0; i < nameSegments.Length - 2; i++)
				{
					if (string.IsNullOrWhiteSpace(prefixParent))
					{
						prefixParent = nameSegments[i] + "/";
					}
					else
					{
						prefixParent = string.Format("{0}{1}/", prefixParent, nameSegments[i]);
					}
				}
			}
			else
			{
				prefixParent = string.Empty;
			}

			return prefixParent;
		}

		/// <summary>
		/// Get the name of the blob without the prefix.
		/// </summary>
		/// <param name="nameSegments">All the segments of the blob name.</param>
		/// <returns>The name with extensions of the blob.</returns>
		private static string GetBlobName(string blobName)
		{
			var nameSegments = GetBlobNameSegments(blobName);

			if (nameSegments.Length > 1)
			{
				// Get the name
				return nameSegments[nameSegments.Length - 1];
			}
			else
			{
				return nameSegments[nameSegments.Length - 1];
			}
		}

		private static string[] GetBlobNameSegments(string blobName)
		{
			var nameSegments = blobName.Split(new char[] { '/' });

			if (nameSegments == null || nameSegments.Length == 0)
				throw new ArgumentException("Segments must at least contain 1 segment", "nameSegments");

			return nameSegments;
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

	public class ContainerViewModel
	{

		private readonly List<BlobItemViewModel> blobs = new List<BlobItemViewModel>();

		public List<BlobItemViewModel> Blobs
		{
			get
			{
				return blobs;
			}
		}

		/// <summary>
		/// The prefix of the current directory. With this you could refresh the directory
		/// </summary>
		public string PrefixFull { get; set; }

		public string PrefixParentFull { get; set; }

		/// <summary>
		/// The complete breadcrumb for the current directory. Must not be used for browsing or as prefix for navigation.
		/// </summary>
		public string BreadCrumb
		{
			get
			{
				//var firstBlob = blobs.FirstOrDefault(b => b.BlobType == FileController.BlobItemType.CloudBlockBlob);

				//string breadCrumb = string.Empty;

				//if (firstBlob == null)
				//{
				//	breadCrumb = ContainerName;
				//}
				//else
				//{
				//	breadCrumb = string.Format("{0}/{1}", ContainerName, firstBlob.PrefixFull);
				//}

				//return breadCrumb;

				string breadCrumb = string.Format("{0}/{1}", ContainerName, PrefixFull);
				if (breadCrumb.Last() == '/')
				{
					breadCrumb = breadCrumb.Remove(breadCrumb.Length - 1, 1);
				}

				return breadCrumb;
			}
		}

		public string ContainerName { get; set; }

		public bool IsRootDirectory { get; set; }

		public bool IsPrivateContainer { get; set; }

	}

	public class BlobItemViewModel
	{

		public FileController.BlobItemType BlobType { get; set; }

		public string Name { get; set; }

		public string Uri { get; set; }

		public string PrefixLast { get; set; }

		public string PrefixFull { get; set; }

		public string Extension
		{
			get
			{
				return Path.GetExtension(Uri);
			}
		}

		public string FileType
		{
			get
			{
				if (Extension == ".jpg" || Extension == ".png" || Extension == ".gif")
				{
					return "Afbeelding";
				}
				else
				{
					return string.Empty;
				}
			}
		}

		public bool IsDirectory
		{
			get
			{
				return BlobType == FileController.BlobItemType.CloudBlobDirectory;
			}
		}

		public bool IsFile
		{
			get
			{
				return BlobType == FileController.BlobItemType.CloudBlockBlob;
			}
		}
	}

	public class CreateContainerViewModel
	{
		[Required]
		[RegularExpression("[a-zA-Z0-9]+")]
		public string Name { get; set; }
	}
}