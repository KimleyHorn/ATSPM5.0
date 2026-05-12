#region license
// Copyright 2026 Utah Departement of Transportation
// for DatabaseInstaller - DatabaseInstaller.Commands/ImportDetectionTypeDetectorCommand.cs
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

using DatabaseInstaller.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Hosting;
using System.CommandLine.NamingConventionBinder;

namespace DatabaseInstaller.Commands
{
    public class ImportDetectionTypeDetectorCommand : Command, ICommandOption<ImportDetectionTypeDetectorCommandConfiguration>
    {
        public ImportDetectionTypeDetectorCommand() : base("import-detection-type-detectors", "Import detector detection type relationships")
        {
            AddOption(SourceOption);
            AddOption(ConfigConnectionOption);
            AddOption(ClearOption);
        }

        public Option<string> SourceOption { get; set; } = new("--source", "Connection string for the source SQL Server") { IsRequired = true };
        public Option<string> ConfigConnectionOption { get; set; } = new("--config-connection", "Connection string for ConfigContext (optional - uses appsettings.json if not provided)");
        public Option<bool> ClearOption { get; set; } = new("--clear", "Delete existing DetectionTypeDetector relationships before importing");

        public ModelBinder<ImportDetectionTypeDetectorCommandConfiguration> GetOptionsBinder()
        {
            var binder = new ModelBinder<ImportDetectionTypeDetectorCommandConfiguration>();

            binder.BindMemberFromValue(b => b.Source, SourceOption);
            binder.BindMemberFromValue(b => b.ConfigConnection, ConfigConnectionOption);
            binder.BindMemberFromValue(b => b.Clear, ClearOption);

            return binder;
        }

        public void BindCommandOptions(HostBuilderContext host, IServiceCollection services)
        {
            services.AddSingleton(GetOptionsBinder());
            services.AddOptions<ImportDetectionTypeDetectorCommandConfiguration>().BindCommandLine();
            services.AddHostedService<ImportDetectionTypeDetectorHostedService>();
        }
    }

    public class ImportDetectionTypeDetectorCommandConfiguration
    {
        public string Source { get; set; }
        public string ConfigConnection { get; set; }
        public bool Clear { get; set; }
    }
}
