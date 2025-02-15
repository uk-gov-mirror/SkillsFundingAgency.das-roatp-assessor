﻿using System.Collections.Generic;
using System.Threading.Tasks;
using SFA.DAS.RoatpAssessor.Web.ApplyTypes.Moderator;
using SFA.DAS.RoatpAssessor.Web.Infrastructure.ApiClients;
using SFA.DAS.RoatpAssessor.Web.ViewModels;

namespace SFA.DAS.RoatpAssessor.Web.Services
{
    public class ModeratorDashboardOrchestrator : IModeratorDashboardOrchestrator
    {
        private readonly IRoatpApplicationApiClient _applicationApiClient;

        public ModeratorDashboardOrchestrator(IRoatpApplicationApiClient applicationApiClient)
        {
            _applicationApiClient = applicationApiClient;
        }

        public async Task<InModerationApplicationsViewModel> GetInModerationApplicationsViewModel(string userId)
        {
            var applicationSummary = await _applicationApiClient.GetApplicationCounts(userId);
            var applications = await _applicationApiClient.GetInModerationApplications(userId);

            var viewModel = new InModerationApplicationsViewModel(userId, applicationSummary.NewApplications, applicationSummary.InProgressApplications, applicationSummary.ModerationApplications, applicationSummary.ClarificationApplications, applicationSummary.ClosedApplications);
            AddApplicationsToViewModel(viewModel, applications);
            return viewModel;
        }

        private void AddApplicationsToViewModel(InModerationApplicationsViewModel viewModel, List<ModerationApplicationSummary> applications)
        {
            foreach (var application in applications)
            {
                var applicationVm = CreateApplicationViewModel(application);
                viewModel.AddApplication(applicationVm);
            }
        }

        private ModerationApplicationViewModel CreateApplicationViewModel(ModerationApplicationSummary application)
        {
            var viewModel = new ModerationApplicationViewModel();

            viewModel.ApplicationId = application.ApplicationId;
            viewModel.ApplicationReferenceNumber = application.ApplicationReferenceNumber;
            viewModel.Assessor1Name = application.Assessor1Name;
            viewModel.Assessor2Name = application.Assessor2Name;
            viewModel.ProviderRoute = application.ProviderRoute;
            viewModel.OrganisationName = application.OrganisationName;
            viewModel.Ukprn = application.Ukprn;
            viewModel.SubmittedDate = application.SubmittedDate;
            viewModel.ApplicationStatus = application.ApplicationStatus;
            viewModel.Assessor1UserId = application.Assessor1UserId;
            viewModel.Assessor2UserId = application.Assessor2UserId;
            viewModel.ModerationStatus = application.ModerationStatus;
            viewModel.ModeratorName = application.ModeratorName;

            return viewModel;
        }
    }
}
