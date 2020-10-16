﻿using SFA.DAS.RoatpAssessor.Web.ApplyTypes.Moderator;
using SFA.DAS.RoatpAssessor.Web.Domain;
using SFA.DAS.RoatpAssessor.Web.Infrastructure.ApiClients;
using SFA.DAS.RoatpAssessor.Web.Models;
using SFA.DAS.RoatpAssessor.Web.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SFA.DAS.RoatpAssessor.Web.Services
{
    public class ClarificationOverviewOrchestrator : IClarificationOverviewOrchestrator
    {
        private readonly IRoatpApplicationApiClient _applicationApiClient;
        private readonly IRoatpModerationApiClient _moderationApiClient;

        public ClarificationOverviewOrchestrator(IRoatpApplicationApiClient applicationApiClient, IRoatpModerationApiClient moderationApiClient)
        {
            _applicationApiClient = applicationApiClient;
            _moderationApiClient = moderationApiClient;
        }

        public async Task<ClarifierApplicationViewModel> GetOverviewViewModel(GetClarificationOverviewRequest request)
        {
            var application = await _applicationApiClient.GetApplication(request.ApplicationId);
            var contact = await _applicationApiClient.GetContactForApplication(request.ApplicationId);
            var sequences = await _moderationApiClient.GetModeratorSequences(request.ApplicationId);

            if (application is null || contact is null || sequences is null)
            {
                return null;
            }

            var viewmodel = new ClarifierApplicationViewModel(application, contact, sequences, request.UserId);

            var savedOutcomes = await _moderationApiClient.GetAllModeratorPageReviewOutcomes(request.ApplicationId, request.UserId);
            if (savedOutcomes is null || !savedOutcomes.Any())
            {
                viewmodel.IsReadyForClarificationConfirmation = false;
            }
            else
            {
                // POTENTIAL TECH DEBT: Decide if processing of sequences should be contained within ClarifierApplicationViewModel rather than modifying this from outside.
                // This would result in better encapsulation of the logic but may cause issues if we need to inspect other sources
                foreach (var sequence in viewmodel.Sequences)
                {
                    foreach (var section in sequence.Sections)
                    {
                        if (string.IsNullOrEmpty(section.Status))
                        {
                            if (sequence.SequenceNumber == SequenceIds.DeliveringApprenticeshipTraining && section.SectionNumber == SectionIds.DeliveringApprenticeshipTraining.YourSectorsAndEmployees)
                            {
                                section.Status = GetSectorsSectionStatus(savedOutcomes);
                            }
                            else
                            {
                                section.Status = GetSectionStatus(savedOutcomes, sequence.SequenceNumber, section.SectionNumber);
                            }
                        }
                    }
                }

                viewmodel.IsReadyForClarificationConfirmation = IsReadyForClarificationConfirmation(viewmodel);
            }

            return viewmodel;
        }

        public string GetSectionStatus(List<ModeratorPageReviewOutcome> pageReviewOutcomes, int sequenceNumber, int sectionNumber)
        {
            var sectionPageReviewOutcomes = pageReviewOutcomes?.Where(p =>
                p.SequenceNumber == sequenceNumber &&
                p.SectionNumber == sectionNumber).ToList();

            var sectionStatus = string.Empty;

            if (sectionPageReviewOutcomes != null && sectionPageReviewOutcomes.Any())
            {
                if (sectionPageReviewOutcomes.Any(p => p.Status == ModeratorPageReviewStatus.InProgress))
                {
                    sectionStatus = ModeratorSectionStatus.InProgress;
                }
                else if (sectionPageReviewOutcomes.Any(p => p.Status == ModeratorPageReviewStatus.Clarification))
                {
                    sectionStatus = ModeratorSectionStatus.Clarification;
                }
                else if (sectionPageReviewOutcomes.All(x => x.Status == ModeratorPageReviewStatus.Pass))
                {
                    sectionStatus = ModeratorSectionStatus.Pass;
                }
                else if (sectionPageReviewOutcomes.All(p =>
                    p.Status == ModeratorPageReviewStatus.Pass || p.Status == ModeratorPageReviewStatus.Fail))
                {
                    sectionStatus = ModeratorPageReviewStatus.Fail;
                }
                else
                {
                    sectionStatus = ModeratorSectionStatus.InProgress;
                }
            }

            return sectionStatus;
        }

        public string GetSectorsSectionStatus(List<ModeratorPageReviewOutcome> pageReviewOutcomes)
        {
            var sectionPageReviewOutcomes = pageReviewOutcomes?.Where(p =>
                p.SequenceNumber == SequenceIds.DeliveringApprenticeshipTraining &&
                p.SectionNumber == SectionIds.DeliveringApprenticeshipTraining.YourSectorsAndEmployees).ToList();

            var sectionStatus = string.Empty;

            if (sectionPageReviewOutcomes != null && sectionPageReviewOutcomes.Any())
            {
                if (sectionPageReviewOutcomes.Any(p => p.Status == ModeratorPageReviewStatus.InProgress))
                {
                    sectionStatus = ModeratorSectionStatus.InProgress;
                }
                else if(sectionPageReviewOutcomes.Any(p => p.Status == ModeratorPageReviewStatus.Clarification))
                {
                    sectionStatus = ModeratorSectionStatus.Clarification;
                }
                else if (sectionPageReviewOutcomes.All(p => p.Status == ModeratorPageReviewStatus.Pass))
                {
                    sectionStatus = ModeratorSectionStatus.Pass;
                }
                else if (sectionPageReviewOutcomes.All(p =>
                            p.Status == ModeratorPageReviewStatus.Pass || p.Status == ModeratorPageReviewStatus.Fail))
                {
                    sectionStatus = ModeratorSectionStatus.Fail;
                }
                else
                {
                    sectionStatus = ModeratorSectionStatus.InProgress;
                }
            }

            return sectionStatus;
        }

        private static bool IsReadyForClarificationConfirmation(ClarifierApplicationViewModel viewmodel)
        {
            var isReadyForClarificationConfirmation = true;

            foreach (var sequence in viewmodel.Sequences)
            {
                foreach (var section in sequence.Sections)
                {
                    if (string.IsNullOrEmpty(section.Status) || (!section.Status.Equals(ModeratorSectionStatus.Pass) &&
                                                   !section.Status.Equals(ModeratorSectionStatus.Fail) &&
                                                   !section.Status.Equals(ModeratorSectionStatus.NotRequired)))
                    {
                        isReadyForClarificationConfirmation = false;
                        break;
                    }
                }

                if (!isReadyForClarificationConfirmation) break;
            }

            return isReadyForClarificationConfirmation;
        }
    }
}
