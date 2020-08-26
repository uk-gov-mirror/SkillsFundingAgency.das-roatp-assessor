﻿using SFA.DAS.RoatpAssessor.Web.ApplyTypes.Apply;
using SFA.DAS.RoatpAssessor.Web.ViewModels;
using System.Threading.Tasks;

namespace SFA.DAS.RoatpAssessor.Web.Services
{
    public interface IModeratorSectionReviewOrchestrator
    {
        Task<ModeratorReviewAnswersViewModel> GetReviewAnswersViewModel(GetReviewAnswersRequest request);

        Task<ApplicationSectorsViewModel> GetSectorsViewModel(GetSectorsRequest request);

        Task<ModeratorSectorDetailsViewModel> GetSectorDetailsViewModel(GetSectorDetailsRequest request);
    }
}
