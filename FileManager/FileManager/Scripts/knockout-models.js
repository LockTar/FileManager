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

    function ContainerBlobsViewModel(data, containerName) {
        var self = this;
        self.ContainerName = containerName;

        self.Mapping = {
            //'observe': [], // Covert the properties to non observable properties
            'Blobs': {
                create: function (options) {
                    return new BlobItemViewModel(options.data, self);
                }
            }
        };

        koMap.fromJS(data, self.Mapping, self);

        self.navigateToDirectory = function (directory) {
            // Get content of the selected directory
            var prefix;
            if (directory) {
                prefix = directory.PrefixFull();
            }

            $.getJSON("File/GetContainerBlobs",
                { containerName: self.ContainerName, prefix: prefix },
                function (data, textStatus, jqXHR) {
                    var vm = new ContainerBlobsViewModel(data, self.ContainerName);

                    self.Blobs(vm.Blobs());
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
    }

    function BlobItemViewModel(data, container) {
        var self = this;
        self.Container = container;

        koMap.fromJS(data, {}, self);
    }

    function UploadsViewModel() {
        //var self = this;

        this.uploads = ko.observableArray([]);
        this.showTotalProgress = ko.observable();
        this.uploadSpeedFormatted = ko.observable();
        this.timeRemainingFormatted = ko.observable();
        this.totalProgress = ko.observable();
    }

    return {


        GetContainerBlobs: function (containerName) {
            $.getJSON("File/GetContainerBlobs",
                { containerName: containerName },
                function (data, textStatus, jqXHR) {
                    var vm = new ContainerBlobsViewModel(data, containerName);

                    var context2 = document.getElementById('blobList');
                    ko.applyBindings(vm, context2);
                }
            );
        },

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



        PageViewModel: function (containerName) {
            ////this.containersViewModel = GetContainerBlobs(containerName);
            ////this.uploadsViewModel = new this.UploadsViewModel();

            ////var vm = {
            ////    containersViewModel: new this.GetContainerBlobs(containerName),
            ////    uploadsViewModel: new this.UploadsViewModel()
            ////};
            var self = this;
            self.containersViewModel = ko.observable();

            $.getJSON("File/GetContainerBlobs",
                { containerName: containerName },
                function (data, textStatus, jqXHR) {
                    var vm = new ContainerBlobsViewModel(data, containerName);
                    self.containersViewModel(vm);
                }
            );

            //this.uploadsViewModel = {
            //    uploads: ko.observableArray([]),
            //    showTotalProgress: ko.observable(),
            //    uploadSpeedFormatted: ko.observable(),
            //    timeRemainingFormatted: ko.observable(),
            //    totalProgress: ko.observable()
            //}

            self.uploadsViewModel = new UploadsViewModel();
        },



        applyBindings: function (model, context) {
            ko.applyBindings(model, context);
        }
    };

});
