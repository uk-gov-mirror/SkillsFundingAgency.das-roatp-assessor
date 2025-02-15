﻿using Moq;
using NUnit.Framework;
using SFA.DAS.AdminService.Common.Extensions;
using SFA.DAS.AdminService.Common.Testing.MockedObjects;
using SFA.DAS.RoatpAssessor.Web.ApplyTypes.Apply;
using SFA.DAS.RoatpAssessor.Web.ApplyTypes.Clarification;
using SFA.DAS.RoatpAssessor.Web.ApplyTypes.Common;
using SFA.DAS.RoatpAssessor.Web.Infrastructure.ApiClients;
using SFA.DAS.RoatpAssessor.Web.Models;
using SFA.DAS.RoatpAssessor.Web.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace SFA.DAS.RoatpAssessor.Web.UnitTests.Services.ClarificationOverviewOrchestrator
{
    [TestFixture]
    public class ClarificationOverviewOrchestratorTests
    {
        private readonly Guid _applicationId = Guid.NewGuid();
        private readonly ClaimsPrincipal _user = MockedUser.Setup();

        private Mock<IRoatpApplicationApiClient> _applicationApiClient;
        private Mock<IRoatpClarificationApiClient> _clarificationApiClient;
        private Web.Services.ClarificationOverviewOrchestrator _orchestrator;
        private string _userId => _user.UserId();
        private string _userDisplayName => _user.UserDisplayName();
        private Apply _application;
        private Contact _contact;
        private List<ClarificationSequence> _sequences;
        private List<ClarificationPageReviewOutcome> _outcomes;


        [SetUp]
        public void SetUp()
        {
            _applicationApiClient = new Mock<IRoatpApplicationApiClient>();
            _clarificationApiClient = new Mock<IRoatpClarificationApiClient>();
            _orchestrator = new Web.Services.ClarificationOverviewOrchestrator(_applicationApiClient.Object, _clarificationApiClient.Object);

            _application = new Apply
            {
                ApplicationId = _applicationId,
                ModerationStatus = ModerationStatus.ClarificationSent,
                Assessor1ReviewStatus = AssessorReviewStatus.Approved,
                Assessor1UserId = _userId,
                Assessor1Name = _userDisplayName,
                Assessor2ReviewStatus = AssessorReviewStatus.Approved,
                Assessor2UserId = $"{ _userId }-2",
                Assessor2Name = $"{ _userDisplayName }-2",
                ApplyData = new ApplyData
                {
                    ModeratorReviewDetails = new ModeratorReviewDetails
                    {
                        ClarificationRequestedOn = DateTime.Now,
                        ModeratorUserId = _userId,
                        ModeratorName = _userDisplayName,
                        ModeratorComments = null
                    }
                }
            };

            _contact = new Contact { Email = "email@address.com" };
            _sequences = new List<ClarificationSequence>();
            _outcomes = new List<ClarificationPageReviewOutcome>();

            _applicationApiClient.Setup(x => x.GetApplication(_applicationId)).ReturnsAsync(_application);
            _applicationApiClient.Setup(x => x.GetContactForApplication(_applicationId)).ReturnsAsync(_contact);
            _clarificationApiClient.Setup(x => x.GetClarificationSequences(_applicationId)).ReturnsAsync(_sequences);
            _clarificationApiClient.Setup(x => x.GetAllClarificationPageReviewOutcomes(_applicationId, _userId)).ReturnsAsync(_outcomes);
        }

        [Test]
        public async Task GetOverviewViewModel_WhenApplicationNotFound_ThenNoViewModelIsReturned()
        {
            var applicationId = Guid.NewGuid();
            _applicationApiClient.Setup(x => x.GetApplication(applicationId)).ReturnsAsync((Apply)null);

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(applicationId, "userId"));

            Assert.IsNull(result);
        }

        [Test]
        public async Task GetOverviewViewModel_WhenApplicationContactNotFound_ThenNoViewModelIsReturned()
        {
            var applicationId = Guid.NewGuid();
            _applicationApiClient.Setup(x => x.GetApplication(applicationId)).ReturnsAsync(new Apply { ApplicationId = applicationId });
            _applicationApiClient.Setup(x => x.GetContactForApplication(applicationId)).ReturnsAsync((Contact)null);

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(applicationId, "userId"));

            Assert.IsNull(result);
        }

        [Test]
        public async Task GetOverviewViewModel_WhenSequencesNotFound_ThenNoViewModelIsReturned()
        {
            var applicationId = Guid.NewGuid();
            _applicationApiClient.Setup(x => x.GetApplication(applicationId)).ReturnsAsync(new Apply { ApplicationId = applicationId });
            _applicationApiClient.Setup(x => x.GetContactForApplication(applicationId)).ReturnsAsync(new Contact());
            _clarificationApiClient.Setup(x => x.GetClarificationSequences(applicationId)).ReturnsAsync((List<ClarificationSequence>)null);

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(applicationId, "userId"));

            Assert.IsNull(result);
        }

        [Test]
        public async Task GetOverviewViewModel_WhenThereAreNoSavedOutcomes_ThenTheApplicationIsNotReadyForClarificationConfirmation()
        {
            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(_applicationId, _userId));

            AssertCommonProperties(result);
            Assert.IsFalse(result.IsReadyForClarificationConfirmation);
        }

        private void AssertCommonProperties(ClarifierApplicationViewModel result)
        {
            Assert.AreEqual(_application.Id, result.Id);
            Assert.AreEqual(_application.ApplicationId, result.ApplicationId);
            Assert.AreEqual(_application.OrganisationId, result.OrgId);
            Assert.AreEqual(_contact.Email, result.ApplicantEmailAddress);
            Assert.AreEqual(_application.ApplicationStatus, result.ApplicationStatus);
            Assert.AreEqual(_application.ModerationStatus, result.ModerationStatus);
            Assert.AreEqual(_application.Assessor1Name, result.Assessor1Name);
            Assert.AreEqual(_application.Assessor2Name, result.Assessor2Name);
            Assert.AreEqual(_application.ApplyData.ModeratorReviewDetails.ModeratorName, result.ModeratorName);
            Assert.AreEqual(_application.ApplyData.ModeratorReviewDetails.ClarificationRequestedOn, result.ClarificationRequestedDate);
            Assert.AreSame(_sequences, result.Sequences);
        }

        [Test]
        public async Task GetOverviewViewModel_When_SavedOutcomes_Contains_Clarification_Then_IsReadyForClarificationConfirmation_IsFalse()
        {
            var expectedStatus = SectionStatus.Clarification;
            _sequences.Add(new ClarificationSequence { SequenceNumber = 1, Sections = new List<ClarificationSection> { new ClarificationSection { SectionNumber = 2 } } });
            _outcomes.Add(new ClarificationPageReviewOutcome { SequenceNumber = 1, SectionNumber = 2, Status = expectedStatus });

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(_applicationId, _userId));

            AssertCommonProperties(result);
            Assert.AreEqual(result.Sequences.First().Sections.First().Status, expectedStatus);
            Assert.IsFalse(result.IsReadyForClarificationConfirmation);
        }

        [Test]
        public async Task GetOverviewViewModel_When_SavedOutcomes_Contains_InProgress_Then_IsReadyForClarificationConfirmation_IsFalse()
        {
            var expectedStatus = SectionStatus.InProgress;
            _sequences.Add(new ClarificationSequence { SequenceNumber = 1, Sections = new List<ClarificationSection> { new ClarificationSection { SectionNumber = 2 } } });
            _outcomes.Add(new ClarificationPageReviewOutcome { SequenceNumber = 1, SectionNumber = 2, Status = expectedStatus });

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(_applicationId, _userId));

            AssertCommonProperties(result);
            Assert.AreEqual(result.Sequences.First().Sections.First().Status, expectedStatus);
            Assert.IsFalse(result.IsReadyForClarificationConfirmation);
        }

        [Test]
        public async Task GetOverviewViewModel_When_SavedOutcomes_AreAll_Pass_Then_IsReadyForClarificationConfirmation_IsTrue()
        {
            var expectedStatus = SectionStatus.Pass;
            _sequences.Add(new ClarificationSequence { SequenceNumber = 1, Sections = new List<ClarificationSection> { new ClarificationSection { SectionNumber = 2 } } });
            _outcomes.Add(new ClarificationPageReviewOutcome { SequenceNumber = 1, SectionNumber = 2, Status = expectedStatus });

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(_applicationId, _userId));

            AssertCommonProperties(result);
            Assert.AreEqual(result.Sequences.First().Sections.First().Status, expectedStatus);
            Assert.IsTrue(result.IsReadyForClarificationConfirmation);
        }

        [Test]
        public async Task GetOverviewViewModel_When_SavedOutcomes_AreAll_Fail_Then_IsReadyForClarificationConfirmation_IsTrue()
        {
            var expectedStatus = SectionStatus.Fail;
            _sequences.Add(new ClarificationSequence { SequenceNumber = 1, Sections = new List<ClarificationSection> { new ClarificationSection { SectionNumber = 2 } } });
            _outcomes.Add(new ClarificationPageReviewOutcome { SequenceNumber = 1, SectionNumber = 2, Status = expectedStatus });

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(_applicationId, _userId));

            AssertCommonProperties(result);
            Assert.AreEqual(result.Sequences.First().Sections.First().Status, expectedStatus);
            Assert.IsTrue(result.IsReadyForClarificationConfirmation);
        }

        [Test]
        public async Task GetOverviewViewModel_When_SavedOutcomes_AreAll_PassOrFail_Then_IsReadyForClarificationConfirmation_IsTrue()
        {
            var expectedStatus = SectionStatus.Fail;

            _sequences.Add(new ClarificationSequence { SequenceNumber = 1, Sections = new List<ClarificationSection> { new ClarificationSection { SectionNumber = 2 } } });
            _outcomes.Add(new ClarificationPageReviewOutcome { SequenceNumber = 1, SectionNumber = 2, Status = ClarificationPageReviewStatus.Pass });
            _outcomes.Add(new ClarificationPageReviewOutcome { SequenceNumber = 1, SectionNumber = 2, Status = ClarificationPageReviewStatus.Fail });

            var result = await _orchestrator.GetOverviewViewModel(new GetClarificationOverviewRequest(_applicationId, _userId));

            AssertCommonProperties(result);
            Assert.AreEqual(result.Sequences.First().Sections.First().Status, expectedStatus);
            Assert.IsTrue(result.IsReadyForClarificationConfirmation);
        }
    }
}
