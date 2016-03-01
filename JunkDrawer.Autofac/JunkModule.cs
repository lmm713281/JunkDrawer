﻿#region license
// JunkDrawer
// Copyright 2013 Dale Newman
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//  
//      http://www.apache.org/licenses/LICENSE-2.0
//  
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
#endregion

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autofac;
using Cfg.Net.Contracts;
using Cfg.Net.Ext;
using Cfg.Net.Reader;
using Pipeline;
using Pipeline.Configuration;
using Pipeline.Contracts;
using Pipeline.Desktop;
using Pipeline.Logging.NLog;
using Pipeline.Nulls;
using Pipeline.Provider.Ado;
using Pipeline.Provider.Excel;
using Pipeline.Provider.File;


namespace JunkDrawer.Autofac {
    public class JunkModule : Module {
        private readonly JunkRequest _junkRequest;

        public JunkModule(JunkRequest junkRequest) {
            _junkRequest = junkRequest;
        }

        public static string ProcessName = "JunkDrawer";
        protected override void Load(ContainerBuilder builder) {

            // Cfg-Net Setup for JunkCfg
            builder.RegisterType<SourceDetector>();
            builder.RegisterType<FileReader>();
            builder.RegisterType<WebReader>();

            builder.Register<IReader>(ctx =>
                new DefaultReader(
                    ctx.Resolve<SourceDetector>(),
                    ctx.Resolve<FileReader>(),
                    new ReTryingReader(ctx.Resolve<WebReader>(), 3)
                )
            );

            builder.Register(ctx => {
                var cfg = new JunkCfg(
                    _junkRequest.Configuration,
                    ctx.Resolve<IReader>()
                );
                // modify the input provider based on the file name requested
                var input = cfg.Connections.First();
                input.File = _junkRequest.FileInfo.FullName;
                if (_junkRequest.Extension.StartsWith(".xls", StringComparison.OrdinalIgnoreCase)) {
                    input.Provider = "excel";
                }
                return cfg;
            }).As<JunkCfg>();

            builder.Register((ctx, p) => {
                var root = new Root(new Validators("js", new NullValidator()));
                root.Load(p.Named<string>("cfg"));
                return root;
            }).As<Root>();

            // Junk Drawer Setup
            builder.Register(ctx => new NLogPipelineLogger(ProcessName, LogLevel.Info)).As<IPipelineLogger>();
            builder.Register(ctx => new PipelineContext(ctx.Resolve<IPipelineLogger>(), new Process { Name = ProcessName, Key = ProcessName }.WithDefaults())).As<IContext>();

            builder.Register<ISchemaReader>(ctx => {
                var connection = ctx.Resolve<JunkCfg>().Input();
                var fileInfo = new FileInfo(Path.IsPathRooted(connection.File) ? connection.File : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, connection.File));
                var context = new ConnectionContext(ctx.Resolve<IContext>(), connection);
                var cfg = connection.Provider == "file" ?
                    new FileInspection(context, fileInfo, 100).Create() :
                    new ExcelInspection(context, fileInfo, 100).Create();
                var root = ctx.Resolve<Root>(new NamedParameter("cfg", cfg));
                root.Processes.First().Pipeline = "linq";
                return new SchemaReader(context, new RunTimeRunner(context), root);
            }).As<ISchemaReader>();

            // Write Configuration based on schema results and JunkRequest
            builder.Register<ICreateConfiguration>(c => new JunkConfigurationCreator(c.Resolve<JunkCfg>(), _junkRequest, c.Resolve<ISchemaReader>())).As<ICreateConfiguration>();
            builder.Register(c => c.Resolve<ICreateConfiguration>().Create()).Named<string>("cfg");
            builder.Register<IRunTimeExecute>(c => new RunTimeExecutor(c.Resolve<IContext>())).As<IRunTimeExecute>();

            // Final product is a JunkImporter that executes the action above
            builder.Register(c => {
                var root = c.Resolve<Root>(new NamedParameter("cfg", c.ResolveNamed<string>("cfg")));
                return new JunkImporter(root, c.Resolve<IRunTimeExecute>());
            }).As<JunkImporter>();
        }
    }
}