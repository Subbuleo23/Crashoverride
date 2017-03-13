﻿using System;
using Foundatio.Repositories.Options;
using Foundatio.Utility;

namespace Foundatio.Repositories {
    public static class SetCacheOptionsExtensions {
        internal const string CacheEnabledKey = "@CacheEnabled";

        public static T Cache<T>(this T options, bool enabled = true) where T : ICommandOptions {
            return options.BuildOption(CacheEnabledKey, enabled);
        }

        internal const string CacheKeyKey = "@CacheKey";
        public static T CacheKey<T>(this T options, string cacheKey) where T : ICommandOptions {
            if (String.IsNullOrEmpty(cacheKey))
                return options;

            options.Values.Set(CacheEnabledKey, true);
            return options.BuildOption(CacheKeyKey, cacheKey);
        }

        internal const string DefaultCacheKeyKey = "@DefaultCacheKey";
        public static T DefaultCacheKey<T>(this T options, string defaultCacheKey) where T : ICommandOptions {
            if (String.IsNullOrEmpty(defaultCacheKey))
                return options;

            options.Values.Set(CacheEnabledKey, true);
            return options.BuildOption(DefaultCacheKeyKey, defaultCacheKey);
        }

        internal const string CacheExpiresInKey = "@CacheExpiresIn";
        public static T CacheExpiresIn<T>(this T options, TimeSpan? expiresIn) where T : ICommandOptions {
            if (expiresIn.HasValue)
                return options.BuildOption(CacheExpiresInKey, expiresIn.Value);

            return options;
        }

        public static T CacheExpiresAt<T>(this T options, DateTime? expiresAtUtc) where T : ICommandOptions {
            if (expiresAtUtc.HasValue)
                return options.BuildOption(CacheExpiresInKey, expiresAtUtc.Value.Subtract(SystemClock.UtcNow));

            return options;
        }
    }
}

namespace Foundatio.Repositories.Options {
    public static class ReadCacheOptionsExtensions {
        public static bool ShouldUseCache(this ICommandOptions options) {
            return options.SafeGetOption(SetCacheOptionsExtensions.CacheEnabledKey, false);
        }

        public static bool HasCacheKey(this ICommandOptions options) {
            return options.Values.Contains(SetCacheOptionsExtensions.CacheKeyKey) || options.Values.Contains(SetCacheOptionsExtensions.DefaultCacheKeyKey);
        }

        public static string GetCacheKey(this ICommandOptions options, string defaultCacheKey = null) {
            return options.SafeGetOption<string>(SetCacheOptionsExtensions.CacheKeyKey, defaultCacheKey ?? options.GetDefaultCacheKey());
        }

        public static string GetDefaultCacheKey(this ICommandOptions options) {
            return options.SafeGetOption<string>(SetCacheOptionsExtensions.CacheKeyKey, null);
        }

        public static TimeSpan GetExpiresIn(this ICommandOptions options) {
            return options.SafeGetOption(SetCacheOptionsExtensions.CacheExpiresInKey, RepositoryConstants.DEFAULT_CACHE_EXPIRATION_TIMESPAN);
        }
    }
}

