﻿using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SFA.DAS.RoatpAssessor.Web.ApplyTypes;
using SFA.DAS.RoatpAssessor.Web.ApplyTypes.Assessor;
using SFA.DAS.RoatpAssessor.Web.Domain;
using SFA.DAS.RoatpAssessor.Web.Helpers;
using SFA.DAS.RoatpAssessor.Web.Infrastructure.ApiClients;
using SFA.DAS.RoatpAssessor.Web.Models;
using SFA.DAS.RoatpAssessor.Web.ViewModels;


namespace SFA.DAS.RoatpAssessor.Web.Services
{
    public class AssessorOverviewOrchestrator : IAssessorOverviewOrchestrator
    {
        private readonly IRoatpApplicationApiClient _applicationApiClient;
        private readonly IRoatpAssessorApiClient _assessorApiClient;

        public AssessorOverviewOrchestrator(IRoatpApplicationApiClient applyApiClient, IRoatpAssessorApiClient assessorApiClient)
        {
            _applicationApiClient = applyApiClient;
            _assessorApiClient = assessorApiClient;
        }

        public async Task<AssessorApplicationViewModel> GetOverviewViewModel(GetAssessorOverviewRequest request)
        {
            var application = await _applicationApiClient.GetApplication(request.ApplicationId);
            var contact = await _applicationApiClient.GetContactForApplication(request.ApplicationId);
            var sequences = await _assessorApiClient.GetAssessorSequences(request.ApplicationId);

            if (application is null || contact is null || sequences is null)
            {
                return null;
            }

            var assessorType = AssessorReviewHelper.SetAssessorType(application, request.UserId);

            var viewmodel = new AssessorApplicationViewModel(application, contact, sequences, request.UserId);

            var savedOutcomes = await _assessorApiClient.GetAllAssessorPageReviewOutcomes(request.ApplicationId, (int)assessorType, request.UserId);
            if (savedOutcomes is null || !savedOutcomes.Any())
            {
                viewmodel.IsReadyForModeration = false;
            }
            else
            {
                // TODO: Can this be part of AssessorApplicationViewModel rather than injecting things outside?
                // Inject the statuses into viewmodel
                foreach (var sequence in viewmodel.Sequences)
                {
                    foreach (var section in sequence.Sections)
                    {
                        if (string.IsNullOrEmpty(section.Status))
                        {
                            if (sequence.SequenceNumber == SequenceIds.DeliveringApprenticeshipTraining && section.SectionNumber == SectionIds.DeliveringApprenticeshipTraining.YourSectorsAndEmployees)
                            {
                                var sectorsChosen = await _assessorApiClient.GetChosenSectors(request.ApplicationId, request.UserId);
                                section.Status = GetSectorsSectionStatus(sectorsChosen, savedOutcomes);
                            }
                            else
                            {
                                var sectionPageReviewOutcomes = savedOutcomes.Where(p =>
                                    p.SequenceNumber == sequence.SequenceNumber &&
                                    p.SectionNumber == section.SectionNumber).ToList();

                                section.Status = GetSectionStatus(sectionPageReviewOutcomes);
                            }
                        }
                    }
                }

                viewmodel.IsReadyForModeration = IsReadyForModeration(viewmodel);
            }

            return viewmodel;
        }

        public string GetSectionStatus(List<AssessorPageReviewOutcome> sectionPageReviewOutcomes)
        {
            var sectionStatus = string.Empty;
            if (sectionPageReviewOutcomes != null && sectionPageReviewOutcomes.Any())
            {
                if (sectionPageReviewOutcomes.Count.Equals(1))
                {
                    // The section only has 1 question
                    sectionStatus = sectionPageReviewOutcomes[0].Status;
                }
                else
                {
                    // The section contains multiple question
                    if (sectionPageReviewOutcomes.All(p => string.IsNullOrEmpty(p.Status)))
                    {
                        sectionStatus = null;
                    }
                    else if (sectionPageReviewOutcomes.All(x => x.Status == AssessorPageReviewStatus.Pass))
                    {
                        sectionStatus = AssessorSectionStatus.Pass;
                    }
                    else if (sectionPageReviewOutcomes.All(p =>
                        p.Status == AssessorPageReviewStatus.Pass || p.Status == AssessorPageReviewStatus.Fail))
                    {
                        var failStatusesCount = sectionPageReviewOutcomes.Count(p => p.Status == AssessorPageReviewStatus.Fail);
                        var pluarlisedFailsOutOf = failStatusesCount == 1 ? AssessorSectionStatus.FailOutOf : AssessorSectionStatus.FailsOutOf;

                        sectionStatus = $"{failStatusesCount} {pluarlisedFailsOutOf} {sectionPageReviewOutcomes.Count}";
                    }
                    else
                    {
                        sectionStatus = AssessorSectionStatus.InProgress;
                    }
                }
            }

            return sectionStatus;
        }

        public string GetSectorsSectionStatus(IEnumerable<Sector> sectorsChosen, IEnumerable<AssessorPageReviewOutcome> savedOutcomes)
        {
            var sectionPageReviewOutcomes = savedOutcomes?.Where(p =>
                p.SequenceNumber == SequenceIds.DeliveringApprenticeshipTraining &&
                p.SectionNumber == SectionIds.DeliveringApprenticeshipTraining.YourSectorsAndEmployees).ToList();

            var sectionStatus = string.Empty;
            if (sectionPageReviewOutcomes != null && sectionPageReviewOutcomes.Any())
            {
                if (sectionPageReviewOutcomes.All(p => string.IsNullOrEmpty(p.Status)))
                {
                    sectionStatus = null;
                }
                else if (sectionPageReviewOutcomes.All(p => p.Status == AssessorPageReviewStatus.Pass))
                {
                    sectionStatus = AssessorSectionStatus.Pass;
                }
                else if (sectionPageReviewOutcomes.All(p =>
                            p.Status == AssessorPageReviewStatus.Pass || p.Status == AssessorPageReviewStatus.Fail))
                {
                    sectionStatus = AssessorSectionStatus.Fail;
                }
                else
                {
                    sectionStatus = AssessorSectionStatus.InProgress;
                }
            }

            return sectionStatus;
        }

        private static bool IsReadyForModeration(AssessorApplicationViewModel viewmodel)
        {
            var isReadyForModeration = true;

            foreach (var sequence in viewmodel.Sequences)
            {
                foreach (var section in sequence.Sections)
                {
                    if (string.IsNullOrEmpty(section.Status) || (!section.Status.Equals(AssessorSectionStatus.Pass) &&
                                                   !section.Status.Equals(AssessorSectionStatus.Fail) &&
                                                   !section.Status.Equals(AssessorSectionStatus.NotRequired) &&
                                                   !section.Status.Contains(AssessorSectionStatus.FailOutOf)))
                    {
                        isReadyForModeration = false;
                        break;
                    }
                }

                if (!isReadyForModeration) break;
            }

            return isReadyForModeration;
        }
    }
}
