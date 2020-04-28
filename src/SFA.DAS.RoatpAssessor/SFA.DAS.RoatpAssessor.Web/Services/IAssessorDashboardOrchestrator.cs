﻿using System.Threading.Tasks;
using SFA.DAS.RoatpAssessor.Web.ViewModels;

namespace SFA.DAS.RoatpAssessor.Web.Services
{
    public interface IAssessorDashboardOrchestrator
    {
        Task<NewApplicationsViewModel> GetNewApplicationsViewModel(string userId);
    }
}
