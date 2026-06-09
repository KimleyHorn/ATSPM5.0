#region license
// Copyright 2026 Utah Departement of Transportation
// for EventLogUtility - Utah.Udot.Atspm.EventLogUtility.Commands/DeleteEventsCommand.cs
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.CommandLine;
using System.CommandLine.Binding;
using System.CommandLine.NamingConventionBinder;
using Utah.Udot.Atspm.Infrastructure.Configuration;
using Utah.Udot.Atspm.Infrastructure.Services.HostedServices;

namespace Utah.Udot.Atspm.EventLogUtility.Commands
{
    public class DeleteEventsCommand : Command, ICommandOption
    {
        public DeleteEventsCommand() : base("delete-events", "Delete compressed event logs older than specified days")
        {
            AddArgument(DaysToRetainArg);
            AddGlobalOption(DryRunOption);
        }

        public Argument<int> DaysToRetainArg { get; set; } = 
            new Argument<int>("days", () => 30, "Number of days of data to retain (default: 30)");

        public DryRunOption DryRunOption { get; set; } = new();

        public void BindCommandOptions(HostBuilderContext host, IServiceCollection services)
        {
            services.Configure<DeleteEventsConfiguration>(host.Configuration.GetSection(nameof(DeleteEventsConfiguration)));

            var binder = new ModelBinder<DeleteEventsConfiguration>();
            binder.BindMemberFromValue(b => b.DaysToRetain, DaysToRetainArg);
            binder.BindMemberFromValue(b => b.IsDryRun, DryRunOption);

            services.AddOptions<DeleteEventsConfiguration>()
                .Configure<BindingContext>((config, context) =>
                {
                    binder.UpdateInstance(config, context);
                });

            services.AddHostedService<DeleteExpiredEventsHostedService>();
        }
    }
}
