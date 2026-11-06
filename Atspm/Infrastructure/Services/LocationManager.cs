#region license
// Copyright 2026 Utah Departement of Transportation
// for Infrastructure - Utah.Udot.Atspm.Infrastructure.Services/LocationManager.cs
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
// http://www.apache.org/licenses/LICENSE-2.
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Sockets;
using Utah.Udot.Atspm.Data.Enums;
using Utah.Udot.Atspm.Infrastructure.Extensions;

namespace Utah.Udot.Atspm.Infrastructure.Services
{
    public interface ILocationManager
    {
        Task<Location> CopyLocationToNewVersion(int key, string newVersionLabel);
        Task<Location> CopyLocation(int key, CopyLocationRequest request);
        Task SetLocationToDeleted(int key);
        Task DeleteAllVersions(string locationIdentifier);
    }

    public class CopyLocationRequest
    {
        public string LocationIdentifier { get; set; } = string.Empty;
        public string PrimaryName { get; set; } = string.Empty;
        public string? SecondaryName { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public List<CopyDeviceRequest> Devices { get; set; } = new();
    }

    public class CopyDeviceRequest
    {
        public int SourceDeviceId { get; set; }
        public string? DeviceIdentifier { get; set; }
        public string Ipaddress { get; set; } = string.Empty;
    }

    public class LocationManager : ILocationManager
    {
        private readonly ILocationRepository _locations;
        private readonly IDeviceRepository _devices;

        public LocationManager(ILocationRepository locations, IDeviceRepository devices)
        {
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
            _devices = devices ?? throw new ArgumentNullException(nameof(devices));
        }

        public async Task<Location> CopyLocationToNewVersion(int key, string newVersionLabel)
        {
            var deviceIds = _devices.GetList()
                .Where(w => w.LocationId == key)
                .Select(s => s.Id)
                    .ToList();
            var newLocation = await _locations.CopyLocationToNewVersion(key, newVersionLabel);
            _devices.UpdateDevicesForNewVersion(deviceIds, newLocation.Id);
            return newLocation;
        }

        public async Task<Location> CopyLocation(int key, CopyLocationRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.LocationIdentifier) ||
                request.LocationIdentifier.Trim().Length > 10)
                throw new ArgumentException("A location identifier of 1 to 10 characters is required.");

            var identifier = request.LocationIdentifier.Trim();
            if (await _locations.LocationExists(identifier))
                throw new ArgumentException("Location identifier already exists.");
            if (string.IsNullOrWhiteSpace(request.PrimaryName) || request.PrimaryName.Length > 100 ||
                request.SecondaryName?.Length > 100)
                throw new ArgumentException("A primary name of 100 characters or fewer is required.");
            if (!double.IsFinite(request.Latitude) || request.Latitude < -90 || request.Latitude > 90 ||
                !double.IsFinite(request.Longitude) || request.Longitude < -180 || request.Longitude > 180)
                throw new ArgumentException("Valid latitude and longitude are required.");

            var source = _locations.GetVersionByIdDetached(key);
            if (source == null)
                throw new KeyNotFoundException($"Location {key} was not found.");
            if (source.LocationTypeId != (int)LocationTypes.I)
                throw new ArgumentException("Only intersections can be copied from this form.");

            var sourceDevices = source.Devices.ToDictionary(device => device.Id);
            if (request.Devices == null || request.Devices.Count != sourceDevices.Count ||
                request.Devices.Select(device => device.SourceDeviceId).Distinct().Count() != sourceDevices.Count ||
                request.Devices.Any(device => !sourceDevices.ContainsKey(device.SourceDeviceId) ||
                    !IPAddress.TryParse(device.Ipaddress, out var address) ||
                    address.AddressFamily != AddressFamily.InterNetwork ||
                    device.Ipaddress.Split('.').Length != 4 ||
                    device.Ipaddress.Length > 15))
                throw new ArgumentException("Each copied device needs a valid IP address.");

            var copy = (Location)source.Clone();
            copy.Id = 0;
            copy.LocationIdentifier = identifier;
            copy.PrimaryName = request.PrimaryName?.Trim() ?? string.Empty;
            copy.SecondaryName = request.SecondaryName?.Trim() ?? string.Empty;
            copy.Latitude = request.Latitude;
            copy.Longitude = request.Longitude;
            copy.VersionAction = LocationVersionActions.Initial;
            copy.Start = DateTime.Now;
            copy.Note = $"Copy of Signal {source.LocationIdentifier}";
            copy.Created = null;
            copy.Modified = null;
            copy.Jurisdiction = null;
            copy.Region = null;
            copy.LocationType = null;

            copy.Approaches = source.Approaches.Select(approach =>
            {
                var newApproach = (Approach)approach.Clone();
                newApproach.Id = 0;
                newApproach.LocationId = 0;
                newApproach.Location = copy;
                newApproach.DirectionType = null;
                newApproach.Created = null;
                newApproach.Modified = null;
                newApproach.Detectors = approach.Detectors.Select(detector =>
                {
                    var newDetector = (Detector)detector.Clone();
                    newDetector.Id = 0;
                    newDetector.ApproachId = 0;
                    newDetector.Approach = newApproach;
                    newDetector.Created = null;
                    newDetector.Modified = null;
                    newDetector.DetectorComments = new HashSet<DetectorComment>();
                    return newDetector;
                }).ToList();
                return newApproach;
            }).ToList();

            copy.Devices = request.Devices.Select(deviceRequest =>
            {
                var device = (Device)sourceDevices[deviceRequest.SourceDeviceId].Clone();
                device.Id = 0;
                device.LocationId = 0;
                device.Location = copy;
                device.DeviceConfiguration = null;
                device.DeviceIdentifier = string.IsNullOrWhiteSpace(deviceRequest.DeviceIdentifier)
                    ? (device.DeviceIdentifier == source.LocationIdentifier ? identifier : device.DeviceIdentifier)
                    : deviceRequest.DeviceIdentifier.Trim();
                device.Ipaddress = deviceRequest.Ipaddress.Trim();
                device.Created = null;
                device.Modified = null;
                return device;
            }).ToList();

            await _locations.AddAsync(copy);
            return copy;
        }

        public async Task DeleteAllVersions(string locationIdentifier)
        {
            var versions = _locations.GetList()
                 .Include(i => i.Jurisdiction)
                 .Include(i => i.Region)
                 .Include(i => i.Devices)
                 .FromSpecification(new LocationIdentifierSpecification(locationIdentifier))
                 .FromSpecification(new ActiveLocationSpecification())
                 .ToList();

            foreach (var version in versions)
            {
                if (version.Devices != null)
                {
                    foreach (var device in version.Devices)
                    {
                        _devices.Remove(device);
                    }
                }
                await _locations.SetLocationToDeleted(version.Id);
            }
        }

        public async Task SetLocationToDeleted(int key)
        {
            var location = await _locations.LookupAsync(key);
            var versions = _locations.GetAllVersionsOfLocation(location.LocationIdentifier);
            if (versions.Count() > 1)
            {
                //get the version previous to the one being deleted
                var previousVersion = versions.Where(w => w.Start < location.Start).OrderByDescending(o => o.Start).FirstOrDefault();
                if (previousVersion != null && location.Devices != null)
                {
                    //assign the devices of the deleted version to the location id of the previous version
                    foreach (var device in location.Devices)
                    {
                        device.LocationId = previousVersion.Id;
                    }
                }
            }
            else
            {
                if (location.Devices != null)
                {
                    foreach (var device in location.Devices)
                    {
                        _devices.Remove(device);
                    }
                }
            }
            await _locations.SetLocationToDeleted(key);
        }
    }
}
