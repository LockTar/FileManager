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

	function BlobItemViewModel(data, container) {
		var self = this;
		self.Container = container;

		koMap.fromJS(data, {}, self);


	}

	return {

		GetContainerBlobsViewModel: function (data) {
			var self = this;

			self.Mapping = {
				//'observe': [], // Covert the properties to non observable properties
				'Blobs': {
					create: function (options) {
						return new BlobItemViewModel(options.data, self);
					}
				}
			};


			koMap.fromJS(data, self.Mapping, self);

			self.navigateToDirectory = function (item) {
				// Get content of the selected directory
				$.getJSON("File/GetContainerBlobs",
					{ containerName: "newsarticleimages", prefix: item.PrefixFull() },
					function (data, textStatus, jqXHR) {
						koMap.fromJS(data, self.Mapping, self);
					}
				);
			}
		},

		////GetContainerBlobsViewModel: function (data) {
		////	var self = this;

		////	koMap.fromJS(data, {
		////		ignore: ['Blobs']
		////	}, self);

		////	self.Blobs = new ko.observableArray();

		////	self.navigateToDirectory = function (item) {
		////		// Get content of the selected directory
		////		var prefix = item ? item.PrefixFull() : null;

		////		$.getJSON("File/GetContainerBlobs",
		////			{ containerName: "newsarticleimages", prefix: prefix },
		////			function (data, textStatus, jqXHR) {
		////				self.Blobs(data.Blobs.map(function (b) {
		////					new BlobItemViewModel(b, self);
		////				}));
		////			}
		////		);
		////	}

		////	self.navigateToDirectory();
		////},

		UploadsViewModel: function () {
			this.uploads = ko.observableArray([]);
			this.showTotalProgress = ko.observable();
			this.uploadSpeedFormatted = ko.observable();
			this.timeRemainingFormatted = ko.observable();
			this.totalProgress = ko.observable();
		},

		FileViewModel: function (file) {
			this.file = file;
			this.uploadProgress = ko.observable(0);
			this.uploadCompleted = ko.observable(false);
			this.uploadSpeedFormatted = ko.observable();
			this.fileName = file.fileName;
			this.fileSizeFormated = formatFileSize(file.fileSize);
		},

		applyBindings: function (model, context) {
			ko.applyBindings(model, context);
		}
	};

});
