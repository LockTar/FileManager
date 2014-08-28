/*jslint browser:true*/
/*global define*/

define(['src/html5Upload', 'knockout', 'knockoutMapper'], function (html5Upload, ko, koMap) {
    'use strict';

    // Here's a custom Knockout binding that makes elements shown/hidden via jQuery's fadeIn()/fadeOut() methods
    // Could be stored in a separate utility library
    ko.bindingHandlers.fadeVisible = {
        init: function (element, valueAccessor) {
            // Initially set the element to be instantly visible/hidden depending on the value
            var value = valueAccessor();
            $(element).toggle(ko.utils.unwrapObservable(value)); // Use "unwrapObservable" so we can handle values that may or may not be observable
        },
        update: function (element, valueAccessor) {
            // Whenever the value subsequently changes, slowly fade the element in or out
            var value = valueAccessor();
            ko.utils.unwrapObservable(value) ? $(element).fadeIn() : $(element).fadeOut();
        }
    };

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

    function removeAllBlobs(blobsArray) {
        blobsArray.removeAll();
    }

    function GetContainer(prefix, self) {
        $.getJSON("File/GetContainerBlobs",
            { containerName: self.ContainerName(), prefix: prefix },
            function (data, textStatus, jqXHR) {
                self.ContainerName(data.ContainerName);
                self.IsPrivateContainer(data.IsPrivateContainer);
                self.BreadCrumb(data.BreadCrumb);
                self.PrefixParentFull(data.PrefixParentFull);
                self.IsRootDirectory(data.IsRootDirectory);
                self.PrefixFull(data.PrefixFull);

                removeAllBlobs(self.Blobs);

                ko.utils.arrayForEach(data.Blobs, function (blob) {
                    self.Blobs.push(new BlobItemViewModel(blob, self));
                });
            }
        ).done(function (data, textStatus, jqXHR) {
            console.log("Get container: " + textStatus + " " + jqXHR.status + " " + jqXHR.statusText);
        });
    }

    function ContainerViewModel(containerName, pageViewModel) {
        var self = this;
        self.PageViewModel = pageViewModel;

        self.ContainerName = ko.observable(containerName);
        self.IsPrivateContainer = ko.observable();
        self.BreadCrumb = ko.observable();
        self.PrefixParentFull = ko.observable();
        self.IsRootDirectory = ko.observable();
        self.PrefixFull = ko.observable();
        self.Blobs = ko.observableArray();
        self.NewDirectoryName = ko.observable("");

        self.Update = function (directory) {
            // Get content of the selected directory
            var prefix = null;
            if (directory) {
                prefix = directory.PrefixFull();
            }

            GetContainer(prefix, self);
        }

        self.ParentDirectory = function () {
            GetContainer(self.PrefixParentFull(), self)
        }

        self.RefreshDirectory = function () {
            GetContainer(self.PrefixFull(), self)
        }

        self.DeleteFile = function (file) {
            console.log('Delete file in Container: ' + self.ContainerName() + ' File: ' + file.Name() + ' Prefix: ' + file.PrefixFull());

            // Delete the selected file
            $.post("File/DeleteFile",
				{
				    containerName: self.ContainerName,
				    fileName: file.Name(),
				    prefix: file.PrefixFull()
				}).done(function (data, textStatus, jqXHR) {
				    console.log("Delete file: " + textStatus + " " + jqXHR.status + " " + jqXHR.statusText);

				    // remove the blob from the array
				    self.Blobs.remove(file);
				}).fail(function (jqXHR, textStatus, statusText) {
				    console.log("Delete file: " + textStatus + " " + jqXHR.status + " " + jqXHR.statusText);
				}).always(function (jqXHR, textStatus, jqXHR2) {
				    if (textStatus === "success") {
				        // because jqXHR is switching if action is succeeding
				        jqXHR = jqXHR2;
				    }

				    console.log("Delete file: " + textStatus + " " + jqXHR.status + " " + jqXHR.statusText);
				});
        }

        self.AddDirectory = function () {
            if (self.NewDirectoryName() !== '') {
                GetContainer(self.PrefixFull() + self.NewDirectoryName() + '/', self);
                self.NewDirectoryName('');
                self.PageViewModel.DisableAddDirectoryArea();
            }
        }

        self.Update();
    }

    function BlobItemViewModel(data, container) {
        var self = this;
        self.Container = container;

        koMap.fromJS(data, {}, self);
    }

    function UploadsViewModel(pageViewModel) {
        var self = this;

        self.PageViewModel = pageViewModel;

        self.uploads = ko.observableArray([]);
        self.showTotalProgress = ko.observable();
        self.uploadSpeedFormatted = ko.observable();
        self.timeRemainingFormatted = ko.observable();
        self.totalProgress = ko.observable();
    }

    function FileViewModel(file) {
        this.file = file;
        this.uploadProgress = ko.observable(0);
        this.uploadCompleted = ko.observable(false);
        this.uploadSpeedFormatted = ko.observable();
        this.fileName = file.fileName;
        this.fileSizeFormated = formatFileSize(file.fileSize);
    }

    function ReInitialize(pageViewModel) {
        if (html5Upload.fileApiSupported()) {
            var fileManager = html5Upload.initialize({
                // URL that handles uploaded files
                uploadUrl: '/file/upload',

                pageViewModel: pageViewModel,

                // HTML element on which files should be dropped (optional)
                dropContainer: document.getElementById('dragndropimage'),

                // HTML file input element that allows to select files (optional)
                inputField: document.getElementById('upload-input'),

                // Key for the file data (optional, default: 'file')
                key: 'File',

                // Additional data submitted with file (optional)
                data: {
                    //containerName: pageViewModel.containerViewModel.ContainerName(),
                    //prefix: pageViewModel.containerViewModel.PrefixFull()
                },

                // Maximum number of simultaneous uploads
                // Other uploads will be added to uploads queue (optional)
                maxSimultaneousUploads: 2,

                // Callback for each dropped or selected file
                // It receives one argument, add callbacks 
                // by passing events map object: file.on({ ... })
                onFileAdded: function (file) {
                    var fileModel = new FileViewModel(file);
                    pageViewModel.uploadsViewModel.uploads.push(fileModel);

                    file.on({
                        // Called after received response from the server
                        onCompleted: function (response) {
                            fileModel.uploadCompleted(true);
                        },

                        // Called during upload progress, first parameter
                        // is decimal value from 0 to 100.
                        onProgress: function (progress, fileSize, uploadedBytes) {
                            fileModel.uploadProgress(parseInt(progress, 10));
                        }
                    });
                }
            });

            return fileManager;
        }

        return null;
    }

    return {
        Initialize: function (pageViewModel) {
            var filemanager = ReInitialize(pageViewModel);

            return filemanager;
        },

        PageViewModel: function (containerName) {
            var self = this;

            self.UploadAreaEnabled = ko.observable(false);
            self.EnableUploadArea = function () {
                self.UploadAreaEnabled(true);
                self.DisableAddDirectoryArea();
            }

            self.DisableUploadArea = function () {
                self.UploadAreaEnabled(false);
            }

            self.ToggleUploadArea = function () {
                if (self.UploadAreaEnabled() === false) {
                    self.EnableUploadArea();
                    self.DisableAddDirectoryArea();
                }
                else {
                    self.DisableUploadArea();
                }
            }

            self.AddDirectoryAreaEnabled = ko.observable(false);
            self.NewDirectoryNameFocus = ko.observable(false);

            self.EnableAddDirectoryArea = function () {
                self.DisableUploadArea();
                self.AddDirectoryAreaEnabled(true);
                self.NewDirectoryNameFocus(true);
            }

            self.DisableAddDirectoryArea = function () {
                self.AddDirectoryAreaEnabled(false);
            }

            self.ToggleAddDirectoryArea = function () {
                if (self.AddDirectoryAreaEnabled() === false) {
                    self.DisableUploadArea();
                    self.EnableAddDirectoryArea();
                }
                else {
                    self.DisableAddDirectoryArea();
                }
            }

            self.uploadsViewModel = new UploadsViewModel(self);
            self.containerViewModel = new ContainerViewModel(containerName, self);
        },

        applyBindings: function (model, context) {
            ko.applyBindings(model, context);
        }
    };
});