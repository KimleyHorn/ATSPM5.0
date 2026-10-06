#region license
// Copyright 2026 Utah Departement of Transportation
// for ConfigApi - Utah.Udot.Atspm.ConfigApi.Controllers/DeviceConfigurationController.cs
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

using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using System.Reflection;
using Utah.Udot.Atspm.Data.Models;
using Utah.Udot.Atspm.Data.Models.EventLogModels;
using Utah.Udot.Atspm.Repositories.ConfigurationRepositories;
using Utah.Udot.Atspm.Services;
using static Microsoft.AspNetCore.Http.StatusCodes;
using static Microsoft.AspNetCore.OData.Query.AllowedQueryOptions;

namespace Utah.Udot.Atspm.ConfigApi.Controllers
{
    /// <summary>
    /// Device configuration controller
    /// </summary>
    [ApiVersion(1.0)]
    public class DeviceConfigurationController : LocationPolicyControllerBase<DeviceConfiguration, int>
    {
        private readonly IDeviceConfigurationRepository _repository;

        /// <inheritdoc/>
        public DeviceConfigurationController(IDeviceConfigurationRepository repository) : base(repository)
        {
            _repository = repository;
        }

        #region NavigationProperties

        /// <summary>
        /// <see cref="Device"/> navigation property action
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        [EnableQuery(AllowedQueryOptions = Count | Filter | Select | OrderBy | Top | Skip)]
        [ProducesResponseType(Status200OK)]
        [ProducesResponseType(Status404NotFound)]
        [ProducesResponseType(Status400BadRequest)]
        public ActionResult<IEnumerable<Device>> GetDevices([FromRoute] int key)
        {
            return GetNavigationProperty<IEnumerable<Device>>(key);
        }

        #endregion

        #region Actions

        #endregion

        #region Functions

        /// <summary>
        /// Gets all implementations of <see cref="IEventLogDecoder"/>
        /// that can be assigned to <see cref="DeviceConfiguration"/> for decoding <see cref="EventLogModelBase"/> derived types.
        /// </summary>
        /// <returns>List of <see cref="IEventLogDecoder"/> implementations</returns>
        [HttpGet]
        [EnableQuery(AllowedQueryOptions = Count | Filter | Select | OrderBy | Top | Skip)]
        [ProducesResponseType(typeof(IEnumerable<string>), Status200OK)]
        public IActionResult GetEventLogDecoders()
        {
            var result = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .SelectMany(GetLoadableTypes)
                .Where(w => w.GetInterfaces()
                .Contains(typeof(IEventLogDecoder)))
                .Where(w => !w.IsAbstract)
                .Where(w => !w.IsInterface)
                .Select(s => s.Name)
                .ToList();

            return Ok(result);
        }

        // Some third-party assemblies (e.g. Microsoft.Data.SqlClient) contain types that
        // can't be loaded via reflection; skip those instead of failing the whole scan.
        private static IEnumerable<Type> GetLoadableTypes(Assembly assembly)
        {
            try
            {
                return assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException e)
            {
                return e.Types.OfType<Type>();
            }
        }

        #endregion
    }
}
