﻿namespace Soundfingerprinting.Query.Internal
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using Soundfingerprinting.Fingerprinting.Configuration;
    using Soundfingerprinting.Fingerprinting.WorkUnitBuilder;
    using Soundfingerprinting.Hashing.MinHash;

    internal sealed class FingerprintingQueryUnit : IOngoingQuery, IOngoingQueryConfiguration, IOngoingQueryConfigurationWithFingerprinting, IFingerprintingQueryUnit
    {
        private readonly IFingerprintingUnitsBuilder fingerprintingUnitsBuilder;
        private readonly IQueryFingerprintService queryFingerprintService;
        private readonly IMinHashService minHashService;

        private Func<IWithConfiguration> fingerprintingMethodFromSelector;
        private Func<IFingerprintingUnit> createFingerprintMethod;
        private IQueryConfiguration queryConfiguration;

        public FingerprintingQueryUnit(IFingerprintingUnitsBuilder fingerprintingUnitsBuilder, IQueryFingerprintService queryFingerprintService, IMinHashService minHashService)
        {
            this.fingerprintingUnitsBuilder = fingerprintingUnitsBuilder;
            this.queryFingerprintService = queryFingerprintService;
            this.minHashService = minHashService;
        }

        public IOngoingQueryConfigurationWithFingerprinting From(string pathToAudioFile)
        {
            fingerprintingMethodFromSelector = () => fingerprintingUnitsBuilder.BuildFingerprints().On(pathToAudioFile);
            return this;
        }

        public IOngoingQueryConfigurationWithFingerprinting From(string pathToAudioFile, int millisecondsToProcess, int startAtMillisecond)
        {
            fingerprintingMethodFromSelector = () => fingerprintingUnitsBuilder.BuildFingerprints().On(pathToAudioFile, millisecondsToProcess, startAtMillisecond);
            return this;
        }

        public IOngoingQueryConfigurationWithFingerprinting From(float[] audioSamples)
        {
            fingerprintingMethodFromSelector = () => fingerprintingUnitsBuilder.BuildFingerprints().On(audioSamples);
            return this;
        }

        public IOngoingQueryConfiguration From(bool[] fingerprint)
        {
            fingerprintingMethodFromSelector = () => new EmptyWithConfiguration(fingerprint, minHashService);
            return this;
        }

        public IFingerprintingQueryUnit With(IFingerprintingConfiguration fingerprintingConfiguration, IQueryConfiguration configuration)
        {
            queryConfiguration = configuration;
            createFingerprintMethod = () => fingerprintingMethodFromSelector().With(fingerprintingConfiguration);
            return this;
        }

        public Task<QueryResult> Query()
        {
            return createFingerprintMethod().RunAlgorithm().ContinueWith(task =>
                    {
                        List<bool[]> fingerprints = task.Result;
                        return queryFingerprintService.Query(fingerprints, queryConfiguration).Result;
                    });
        }

        public IFingerprintingQueryUnit With(IQueryConfiguration configuration)
        {
            queryConfiguration = configuration;
            return this;
        }

        private class EmptyWithConfiguration : IWithConfiguration
        {
            private readonly bool[] fingerprint;

            private readonly IMinHashService minHashService;

            public EmptyWithConfiguration(bool[] fingerprint, IMinHashService minHashService)
            {
                this.fingerprint = fingerprint;
                this.minHashService = minHashService;
            }

            public IFingerprintingUnit With(IFingerprintingConfiguration configuration)
            {
                return new EmptyFingerprintingUnit(fingerprint, minHashService);
            }

            public IFingerprintingUnit With<T>() where T : IFingerprintingConfiguration, new()
            {
                return new EmptyFingerprintingUnit(fingerprint, minHashService);
            }

            public IFingerprintingUnit WithCustomConfiguration(Action<CustomFingerprintingConfiguration> transformation)
            {
                return new EmptyFingerprintingUnit(fingerprint, minHashService);
            }

            private class EmptyFingerprintingUnit : IFingerprintingUnit
            {
                private readonly bool[] fingerprint;

                private readonly IMinHashService minHashService;

                public EmptyFingerprintingUnit(bool[] fingerprint, IMinHashService minHashService)
                {
                    this.fingerprint = fingerprint;
                    this.minHashService = minHashService;
                }

                public IFingerprintingConfiguration Configuration { get; set; }

                public Task<List<bool[]>> RunAlgorithm()
                {
                    TaskCompletionSource<List<bool[]>> tcs = new TaskCompletionSource<List<bool[]>>();
                    tcs.SetResult(new List<bool[]> { fingerprint });
                    return tcs.Task;
                }

                public Task<List<byte[]>> RunAlgorithmWithHashing()
                {
                    TaskCompletionSource<List<byte[]>> tcs = new TaskCompletionSource<List<byte[]>>();
                    tcs.SetResult(new List<byte[]> { minHashService.Hash(fingerprint) });
                    return tcs.Task;
                }
            }
        }
    }
}