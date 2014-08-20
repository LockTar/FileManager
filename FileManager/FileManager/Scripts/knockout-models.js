/*jslint browser:true*/
/*global define*/

define(['knockout', 'knockoutMapper'], function (ko, koMap) {
	'use strict';

	function trimTrailingZeros(number) {
		return number.toFixed(1).replace(/\.0+$/, '');
	}

	function formatFileSize(sizeInBytes) {
		var kiloByte = 1024,
            megaByte = Math.pow(kiloByte, 2),
            gigaByte = Math.pow(kiloByte, 3);

		if (sizeInBytes < kiloByte) {
			return sizeInBytes + ' B';
		}

		if (sizeInBytes < megaByte) {
			return trimTrailingZeros(sizeInBytes / kiloByte) + ' kB';
		}

		if (sizeInBytes < gigaByte) {
			return trimTrailingZeros(sizeInBytes / megaByte) + ' MB';
		}

		return trimTrailingZeros(sizeInBytes / gigaByte) + ' GB';
	}

	function ContainerViewModel(containerName) {
		var self = this;

		self.ContainerName = ko.observable(containerName);
		self.IsPrivateContainer = ko.observable();
		self.Blobs = ko.observableArray();

		self.Update = function (directory) {
			// Get content of the selected directory
			var prefix;
			if (directory) {
				prefix = directory.PrefixFull();
			}

			$.getJSON("File/GetContainerBlobs",
                { containerName: self.ContainerName(), prefix: prefix },
                function (data, textStatus, jqXHR) {
                	self.ContainerName(data.CurrentContainerName);
                	self.IsPrivateContainer(data.IsPrivateContainer);

                	self.Blobs.removeAll();

                	ko.utils.arrayForEach(data.Blobs, function (blob) {
                		self.Blobs.push(new BlobItemViewModel(blob, self));
                	});
                }
            );
		}
		
		self.DeleteFile = function (file) {
			console.log('Delete file in Container: ' + self.ContainerName + ' File: ' + file.Name() + ' Prefix: ' + file.PrefixFull());

			// Delete the selected file
			$.post("File/DeleteFile",
				{
					containerName: self.ContainerName,
					fileName: file.Name(),
					prefix: file.PrefixFull()
				},
				function () {
					self.Blobs.remove(file);
				}
			);
		}

		self.Update();
	}

	function BlobItemViewModel(data, container) {
		var self = this;
		self.Container = container;

		koMap.fromJS(data, {}, self);
	}

	function UploadsViewModel() {
		var self = this;

		self.uploads = ko.observableArray([]);
		self.showTotalProgress = ko.observable();
		self.uploadSpeedFormatted = ko.observable();
		self.timeRemainingFormatted = ko.observable();
		self.totalProgress = ko.observable();
	}

	return {
		FileViewModel: function (file) {
			this.file = file;
			this.uploadProgress = ko.observable(0);
			this.uploadCompleted = ko.observable(false);
			this.uploadSpeedFormatted = ko.observable();
			this.fileName = file.fileName;
			this.fileSizeFormated = formatFileSize(file.fileSize);
		},

		PageViewModel: function (containerName) {
			var self = this;

			self.containersViewModel = new ContainerViewModel(containerName);

			self.uploadsViewModel = new UploadsViewModel();
		},

		applyBindings: function (model, context) {
			ko.applyBindings(model, context);
		}
	};
});