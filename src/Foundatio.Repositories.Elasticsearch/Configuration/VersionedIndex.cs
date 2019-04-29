﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Foundatio.Parsers.ElasticQueries.Extensions;
using Foundatio.Repositories.Elasticsearch.Extensions;
using Foundatio.Repositories.Elasticsearch.Jobs;
using Foundatio.Repositories.Extensions;
using Nest;
using Microsoft.Extensions.Logging;

namespace Foundatio.Repositories.Elasticsearch.Configuration {
    public interface IVersionedIndex {
        int Version { get; }
        string VersionedName { get; }
        Task<int> GetCurrentVersionAsync();
        ReindexWorkItem CreateReindexWorkItem(int currentVersion);
        Task ReindexAsync(Func<int, string, Task> progressCallbackAsync = null);
    }

    public class VersionedIndex<T> : Index<T>, IVersionedIndex, IMaintainableIndex where T: class {
        public VersionedIndex(IElasticConfiguration configuration, string name, int version = 1)
            : base(configuration, name) {
            Version = version;
            VersionedName = String.Concat(Name, "-v", Version);
        }

        public int Version { get; }
        public string VersionedName { get; }
        public bool DiscardIndexesOnReindex { get; set; } = true;
        private List<ReindexScript> ReindexScripts { get; } = new List<ReindexScript>();

        private class ReindexScript {
            public int Version { get; set; }
            public string Script { get; set; }
        }

        protected virtual void AddReindexScript(int versionNumber, string script) {
            ReindexScripts.Add(new ReindexScript { Version = versionNumber, Script = script });
        }

        protected void RenameFieldScript(int versionNumber, string originalName, string currentName, bool removeOriginal = true) {
            string script = $"if (ctx._source.containsKey(\'{originalName}\')) {{ ctx._source[\'{currentName}\'] = ctx._source.{originalName}; }}";
            ReindexScripts.Add(new ReindexScript { Version = versionNumber, Script = script });

            if (removeOriginal)
                RemoveFieldScript(versionNumber, originalName);
        }

        protected void RemoveFieldScript(int versionNumber, string fieldName) {
            string script = $"if (ctx._source.containsKey(\'{fieldName}\')) {{ ctx._source.remove(\'{fieldName}\'); }}";
            ReindexScripts.Add(new ReindexScript { Version = versionNumber, Script = script });
        }

        public override async Task ConfigureAsync() {
            if (!await IndexExistsAsync(VersionedName).AnyContext()) {
                if (!await AliasExistsAsync(Name).AnyContext())
                    await CreateIndexAsync(VersionedName, d => ConfigureIndex(d).Aliases(ad => ad.Alias(Name))).AnyContext();
                else
                    await CreateIndexAsync(VersionedName, ConfigureIndex).AnyContext();
            }
        }

        protected virtual async Task CreateAliasAsync(string index, string name) {
            if (await AliasExistsAsync(name).AnyContext())
                return;

            var response = await Configuration.Client.AliasAsync(a => a.Add(s => s.Index(index).Alias(name))).AnyContext();
            if (response.IsValid)
                return;

            if (await AliasExistsAsync(name).AnyContext())
                return;

            _logger.LogErrorRequest(response, "Error creating alias {Name}", name);
            string message = $"Error creating alias {name}: {response.GetErrorMessage()}";
            throw new ApplicationException(message, response.OriginalException);
        }

        protected async Task<bool> AliasExistsAsync(string alias) {
            var response = await Configuration.Client.AliasExistsAsync(Names.Parse(alias)).AnyContext();
            if (response.IsValid)
                return response.Exists;

            _logger.LogErrorRequest(response, "Error checking to see if alias {Name}", alias);
            string message = $"Error checking to see if alias {alias} exists: {response.GetErrorMessage()}";
            throw new ApplicationException(message, response.OriginalException);
        }

        public override async Task DeleteAsync() {
            int currentVersion = await GetCurrentVersionAsync();
            var indexesToDelete = new List<string>();
            if (currentVersion != Version) {
                indexesToDelete.Add(String.Concat(Name, "-v", currentVersion));
                indexesToDelete.Add(String.Concat(Name, "-v", currentVersion, "-error"));
            }
            
            indexesToDelete.Add(VersionedName);
            indexesToDelete.Add(String.Concat(VersionedName, "-error"));
            await DeleteIndexesAsync(indexesToDelete.ToArray()).AnyContext();
        }

        public ReindexWorkItem CreateReindexWorkItem(int currentVersion) {
            var reindexWorkItem = new ReindexWorkItem {
                OldIndex = String.Concat(Name, "-v", currentVersion),
                NewIndex = VersionedName,
                Alias = Name,
                Script = GetReindexScripts(currentVersion),
                TimestampField = GetTimeStampField()
            };

            reindexWorkItem.DeleteOld = DiscardIndexesOnReindex && reindexWorkItem.OldIndex != reindexWorkItem.NewIndex;

            return reindexWorkItem;
        }

        private string GetReindexScripts(int currentVersion) {
            var scripts = ReindexScripts.Where(s => s.Version > currentVersion && Version >= s.Version).OrderBy(s => s.Version).ToList();
            if (scripts.Count == 0)
                return null;

            if (scripts.Count == 1)
                return scripts[0].Script;

            string fullScriptWithFunctions = String.Empty;
            string functionCalls = String.Empty;
            for (int i = 0; i < scripts.Count; i++) {
                var script = scripts[i];
                fullScriptWithFunctions += $"void f{i:000}(def ctx) {{ {script.Script} }}\r\n";
                functionCalls += $"f{i:000}(ctx); ";
            }

            return fullScriptWithFunctions + functionCalls;
        }

        public override async Task ReindexAsync(Func<int, string, Task> progressCallbackAsync = null) {
            int currentVersion = await GetCurrentVersionAsync().AnyContext();
            if (currentVersion < 0 || currentVersion >= Version)
                return;

            var reindexWorkItem = CreateReindexWorkItem(currentVersion);
            var reindexer = new ElasticReindexer(Configuration.Client, _logger);
            await reindexer.ReindexAsync(reindexWorkItem, progressCallbackAsync).AnyContext();
        }

        public virtual async Task MaintainAsync(bool includeOptionalTasks = true) {
            if (await AliasExistsAsync(Name).AnyContext())
                return;

            int currentVersion = await GetCurrentVersionAsync().AnyContext();
            if (currentVersion < 0)
                currentVersion = Version;

            await CreateAliasAsync(String.Concat(Name, "-v", currentVersion), Name).AnyContext();
        }

        /// <summary>
        /// Returns the current index version (E.G., the oldest index version).
        /// </summary>
        /// <returns>-1 if there are no indexes.</returns>
        public virtual async Task<int> GetCurrentVersionAsync() {
            int version = await GetVersionFromAliasAsync(Name).AnyContext();
            if (version >= 0)
                return version;

            var indexes = await GetIndexesAsync().AnyContext();
            if (indexes.Count == 0)
                return Version;

            return indexes.Select(i => i.Version).OrderBy(v => v).First();
        }

        protected virtual async Task<int> GetVersionFromAliasAsync(string alias) {
            var response = await Configuration.Client.GetAliasAsync(a => a.Name(alias)).AnyContext();

            if (response.IsValid && response.Indices.Count > 0) {
                _logger.LogTraceRequest(response);
                return response.Indices.Keys.Select(i => GetIndexVersion(i.Name)).OrderBy(v => v).First();
            }

            _logger.LogErrorRequest(response, "Error getting index version from alias");
            return -1;
        }

        protected virtual int GetIndexVersion(string name) {
            if (String.IsNullOrEmpty(name))
                throw new ArgumentNullException(nameof(name));

            string input = name.Substring($"{Name}-v".Length);
            int index = input.IndexOf('-');
            if (index > 0)
                input = input.Substring(0, index);

            if (Int32.TryParse(input, out var version))
                return version;

            return -1;
        }

        protected virtual async Task<IList<IndexInfo>> GetIndexesAsync(int version = -1) {
            string filter = version < 0 ? $"{Name}-v*" : $"{Name}-v{version}";
            if (this is ITimeSeriesIndex<T>)
                filter += "-*";

            var sw = Stopwatch.StartNew();
            var response = await Configuration.Client.CatIndicesAsync(i => i.Pri().Index(Indices.Index(filter))).AnyContext();
            sw.Stop();

            if (!response.IsValid) {
                _logger.LogErrorRequest(response, "Error getting indices {Indexes}", filter);
                string message = $"Error getting indices: {response.GetErrorMessage()}";
                throw new ApplicationException(message, response.OriginalException);
            }

            _logger.LogTraceRequest(response);
            var indices = response.Records
                .Where(i => version < 0 || GetIndexVersion(i.Index) == version)
                .Select(i => new IndexInfo { DateUtc = GetIndexDate(i.Index), Index = i.Index, Version = GetIndexVersion(i.Index) })
                .OrderBy(i => i.DateUtc)
                .ToList();

            _logger.LogInformation("Retrieved list of {IndexCount} indexes in {Duration:g}", indices.Count, sw.Elapsed);
            return indices;
        }

        protected virtual DateTime GetIndexDate(string name) {
            return DateTime.MaxValue;
        }

        [DebuggerDisplay("{Index} (Date: {DateUtc} Version: {Version} CurrentVersion: {CurrentVersion})")]
        protected class IndexInfo {
            public string Index { get; set; }
            public int Version { get; set; }
            public int CurrentVersion { get; set; } = -1;
            public DateTime DateUtc { get; set; }
        }
    }
}