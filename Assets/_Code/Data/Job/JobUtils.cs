using System;
using System.Collections.Generic;
using Aqua.Profile;
using BeauUtil;

namespace Aqua {
    static public class JobUtils {

        [Flags]
        public enum JobQueryFlags : uint {
            IgnoreLocation = 0x01,
            IncludeCompleted = 0x02,
        }

        /// <summary>
        /// Iterates over all jobs that should be currently visible for the current save state.
        /// </summary>
        static public IEnumerable<PlayerJob> VisibleJobs(JobQueryFlags queryFlags = 0) {
            return VisibleJobs(Save.Current, queryFlags);
        }

        /// <summary>
        /// Iterates over all jobs that should be currently visible.
        /// </summary>
        static public IEnumerable<PlayerJob> VisibleJobs(SaveData saveData, JobQueryFlags queryFlags = 0) {
            JobDB db = Services.Assets.Jobs;
            ListSlice<JobDesc> stationJobs = default;
            ListSlice<JobDesc> commonJobs = default;
            StringHash32 stationId = saveData.Map.CurrentStationId();

            if ((queryFlags & JobQueryFlags.IgnoreLocation) == 0) {
                stationJobs = db.JobsForStation(stationId);
                commonJobs = db.CommonJobs();
            } else {
                commonJobs = new ListSlice<JobDesc>(db.Objects);
            }

            PlayerJob status;
            JobStatusFlags ignoreWithFlags = (queryFlags & JobQueryFlags.IncludeCompleted) != 0 ? 0 : JobStatusFlags.Completed; 
            
            foreach(var job in stationJobs) {
                status = GetJobStatus(job, saveData, true);
                if ((status.Status & JobStatusFlags.Visible) != 0 && ((status.Status & ignoreWithFlags) == 0)) {
                    yield return status;
                }
            }

            foreach(var job in commonJobs) {
                status = GetJobStatus(job, saveData, true);
                if ((status.Status & JobStatusFlags.Visible) != 0 && ((status.Status & ignoreWithFlags) == 0)) {
                    yield return status;
                }
            }

            if ((queryFlags & JobQueryFlags.IgnoreLocation) == 0) {
                status = Save.CurrentJob;
                if (status.IsValid && status.Job.StationId() != stationId) {
                    status = GetJobStatus(status.Job, saveData, false);
                    yield return status;
                }
            }
        }

        /// <summary>
        /// Returns if the given job is available at the current site, and unstarted.
        /// </summary>
        static public bool IsAvailableAndUnstarted(StringHash32 inId, SaveData saveData) {
            var status = GetJobStatus(Assets.Job(inId), saveData);
            return (status.Status & JobStatusFlags.Mask_Available) == JobStatusFlags.Mask_Available
                && (status.Status & JobStatusFlags.Mask_Progress) == 0;
        }

        /// <summary>
        /// Returns summarization across job progress across all stations.
        /// </summary>
        static public JobProgressSummary SummarizeJobProgress(SaveData saveData) {
            JobDB db = Services.Assets.Jobs;

            PlayerJob status;
            JobProgressSummary summary = default;
            summary.Total = (ushort) db.Count();

            foreach(var job in db.Objects) {
                StringHash32 stationId = job.StationId();

                if (!stationId.IsEmpty && !saveData.Map.IsStationUnlocked(stationId)) {
                    continue;
                }

                status = GetJobStatus(job, saveData, true);

                // if in progress
                if ((status.Status & JobStatusFlags.Mask_Progress) != 0) {
                    if ((status.Status & JobStatusFlags.Active) != 0) {
                        summary.HasActive = true;
                    }

                    if ((status.Status & JobStatusFlags.InProgress) != 0) {
                        summary.InProgress++;
                    } else if ((status.Status & JobStatusFlags.Completed) != 0) {
                        summary.Completed++;
                    }
                } else {
                    // available/unlocked
                    if ((status.Status & JobStatusFlags.Mask_Available) == JobStatusFlags.Mask_Available) {
                        summary.Available++;
                    } else if ((status.Status & JobStatusFlags.Unlocked) == 0) {
                        summary.Locked++;
                    }
                }
            }

            return summary;
        }

        /// <summary>
        /// Returns summarization of job progress at a given station.
        /// </summary>
        static public JobProgressSummary SummarizeJobProgress(StringHash32 stationId, SaveData saveData) {
            JobDB db = Services.Assets.Jobs;

            // if site isn't unlocked
            if (!saveData.Map.IsStationUnlocked(stationId)) {
                return default;
            }

            PlayerJob status;
            JobProgressSummary summary = default;

            var jobList = db.JobsForStation(stationId);
            summary.Total = (ushort) jobList.Length;

            foreach(var job in jobList) {
                status = GetJobStatus(job, saveData, true);

                // if in progress
                if ((status.Status & JobStatusFlags.Mask_Progress) != 0) {
                    if ((status.Status & JobStatusFlags.Active) != 0) {
                        summary.HasActive = true;
                    }

                    if ((status.Status & JobStatusFlags.InProgress) != 0) {
                        summary.InProgress++;
                    } else if ((status.Status & JobStatusFlags.Completed) != 0) {
                        summary.Completed++;
                    }
                } else {
                    // available/unlocked
                    if ((status.Status & JobStatusFlags.Mask_Available) == JobStatusFlags.Mask_Available) {
                        summary.Available++;
                    } else if ((status.Status & JobStatusFlags.Unlocked) == 0) {
                        summary.Locked++;
                    }
                }
            }

            return summary;
        }

        /// <summary>
        /// Returns the status of the given job.
        /// </summary>
        static public PlayerJob GetJobStatus(JobDesc job, SaveData saveData, bool ignoreLocation = false) {
            PlayerJob status;
            status.JobId = job.Id();
            status.Job = job;
            status.Status = saveData.Jobs.GetBaseStatus(status.JobId);

            int exp = (int) saveData.Inventory.Exp();

            // if no progress at all, and we're still listed as visible, let's check
            if ((status.Status & JobStatusFlags.Mask_Progress) == 0 && (status.Status & JobStatusFlags.Mask_Available) != 0) {

                // if not required experience, not visible
                int requiredExp = job.RequiredExp();
                if (exp < requiredExp) {
                    status.Status &= ~JobStatusFlags.Mask_Available;
                    return status;
                }

                // if not at station, and we're considering location, not visible
                if (!ignoreLocation) {
                    StringHash32 stationId = job.StationId();
                    if ((status.Status & JobStatusFlags.Active) == 0 && !stationId.IsEmpty && saveData.Map.CurrentStationId() != stationId) {
                        status.Status &= ~JobStatusFlags.Mask_Available;
                        return status;
                    }
                }

                // if we haven't gotten the requisite bestiary entry, not visible
                StringHash32 requiredEntity = job.RequiredBestiaryEntry();
                if (!requiredEntity.IsEmpty && !saveData.Bestiary.HasEntity(requiredEntity)) {
                    status.Status &= ~JobStatusFlags.Mask_Available;
                    return status;
                }

                // if we haven't gotten the requisite scan, not visible
                StringHash32 requiredScan = job.RequiredScanId();
                if (!requiredScan.IsEmpty && !saveData.Inventory.WasScanned(requiredScan)) {
                    status.Status &= ~JobStatusFlags.Mask_Available;
                    return status;
                }

                // if haven't completed the required jobs, not visible
                foreach(var req in job.RequiredJobs()) {
                    if (!saveData.Jobs.IsComplete(req.Id())) {
                        status.Status &= ~JobStatusFlags.Mask_Available;
                        return status;
                    }
                }

                // if special conditions aren't met, not visible
                StringSlice conditions = job.RequiredConditions();
                if (!conditions.IsEmpty && !Services.Data.CheckConditions(conditions)) {
                    status.Status &= ~JobStatusFlags.Mask_Available;
                    return status;
                }

                // if haven't gotten required upgrades, locked
                foreach(var req in job.RequiredUpgrades()) {
                    if (!saveData.Inventory.HasUpgrade(req)) {
                        status.Status &= ~JobStatusFlags.Unlocked;
                        return status;
                    }
                }
            }

            return status;
        }

        /// <summary>
        /// Returns whether unlocking a given upgrade would open up new jobs at the given station
        /// </summary>
        public static bool UpgradeUnlocksJobAtStation(StringHash32 upgradeId, StringHash32 stationId) {
            JobDB db = Services.Assets.Jobs;

            var jobList = db.JobsForStation(stationId);

            PlayerJob status;
            SaveData saveData = Save.Current;

            foreach (var job in jobList) {
                status = GetJobStatus(job, saveData, true);

                int exp = (int)saveData.Inventory.ItemCount(ItemIds.Exp);

                // if not required experience, not visible
                int requiredExp = job.RequiredExp();
                if (exp < requiredExp) {
                    continue;
                }

                // if haven't completed the required jobs, not visible
                bool completedRequired = true;
                foreach (var req in job.RequiredJobs()) {
                    if (!saveData.Jobs.IsComplete(req.Id())) {
                        completedRequired = false;
                        break;
                    }
                }
                if (!completedRequired) {
                    continue;
                }

                // if special conditions aren't met, not visible
                StringSlice conditions = job.RequiredConditions();
                if (!conditions.IsEmpty && !Services.Data.CheckConditions(conditions)) {
                    continue;
                }

                // if haven't gotten required upgrades, locked
                if (job.RequiredUpgrades().Contains(upgradeId) && status.Status != JobStatusFlags.Visible) {
                    // a job which would be unlocked and is visible was found
                    return true;
                }
            }

            // no job in this station would be unlocked by the upgrade
            return false;
        }
    }

    public struct JobProgressSummary {
        public ushort Locked;
        public ushort Available;
        public ushort InProgress;
        public ushort Completed;
        public bool HasActive;
        public ushort Total;
    }
}