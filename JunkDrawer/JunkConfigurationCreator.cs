#region license
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
using System.Collections.Generic;
using System.Linq;
using Cfg.Net.Ext;
using Pipeline.Configuration;
using Pipeline.Contracts;

namespace JunkDrawer {
    public class JunkConfigurationCreator : ICreateConfiguration {
        private readonly JunkCfg _cfg;
        private readonly JunkRequest _request;
        private readonly ISchemaReader _schemaReader;

        public JunkConfigurationCreator(JunkCfg cfg, JunkRequest request, ISchemaReader schemaReader) {
            _cfg = cfg;
            _request = request;
            _schemaReader = schemaReader;
        }

        public string Create() {
            var schema = _schemaReader.Read();
            // create a process based on the schema
            var process = new Process { Name = "JunkDrawer" }.WithDefaults();
            process.Connections.Clear();
            process.Connections.Add(schema.Connection.Clone());
            process.Connections.Add(_cfg.Output());
            process.Entities = schema.Entities;
            foreach (var entity in process.Entities) {
                entity.PrependProcessNameToOutputName = false;
            }
            if (!string.IsNullOrEmpty(_request.TableName)) {
                process.Entities.First().Alias = _request.TableName;
            }
            process.Mode = "init";

            return new Root { Processes = new List<Process> { process } }.WithDefaults().Serialize();
        }
    }
}