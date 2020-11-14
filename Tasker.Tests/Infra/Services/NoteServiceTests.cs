﻿using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ObjectSerializer.JsonService;
using System.IO;
using System.Threading.Tasks;
using Takser.Infra.Options;
using TaskData;
using TaskData.Notes;
using Tasker.App.Resources;
using Tasker.App.Services;
using Tasker.Domain.Communication;
using Tasker.Domain.Models;
using Tasker.Infra.Services;
using Xunit;

namespace Tasker.Tests.Infra.Services
{
    public class NoteServiceTests
    {
        private const string TestFilesDirectory = "TestFiles";
        private const string GeneralNotesDirectoryName = "GeneralNotes";
        private readonly string GeneralNotesDirectoryPath = Path.Combine(TestFilesDirectory, GeneralNotesDirectoryName);

        private readonly INoteFactory mNoteFactory;

        public NoteServiceTests()
        {
            ServiceCollection serviceCollection = new ServiceCollection();
            serviceCollection.UseTaskerDataEntities();
            serviceCollection.UseJsonObjectSerializer();
            ServiceProvider serviceProvider = serviceCollection
                .AddLogging()
                .BuildServiceProvider();

            mNoteFactory = serviceProvider.GetRequiredService<INoteFactory>();
        }

        [Fact]
        public async Task GetAllNotesPaths_AsExpected()
        {
            IOptions<DatabaseConfigurtaion> databaseOptions = Options.Create(new DatabaseConfigurtaion()
            {
                NotesDirectoryPath = GeneralNotesDirectoryPath,
                NotesTasksDirectoryPath = "a"
            });

            INoteService noteService = new NoteService(
                mNoteFactory, databaseOptions, NullLogger<NoteService>.Instance);
            NoteNode noteNode = await noteService.GetNotesStructure().ConfigureAwait(false);

            Assert.Equal(Path.GetFileName(GeneralNotesDirectoryPath), noteNode.Name);
        }

        [Theory]
        [InlineData("generalNote3.txt", "gn3")]
        [InlineData(@"subject1\generalNote2.txt", "This is generel note 2")]
        public async Task GetNote_RealPathWithExtension_NoteFound(string noteRelativePath, string expectedText)
        {
            IOptions<DatabaseConfigurtaion> databaseOptions = Options.Create(new DatabaseConfigurtaion()
            {
                NotesDirectoryPath = GeneralNotesDirectoryPath,
                NotesTasksDirectoryPath = "a"
            });

            string expectedNotePath = Path.Combine(GeneralNotesDirectoryPath, noteRelativePath);

            INoteService noteService = new NoteService(
                mNoteFactory, databaseOptions, NullLogger<NoteService>.Instance);
            IResponse<NoteResource> response = await noteService.GetGeneralNote(noteRelativePath).ConfigureAwait(false);

            Assert.True(response.IsSuccess);
            Assert.Equal(expectedNotePath, response.ResponseObject.Note.NotePath);
            Assert.Equal(expectedText, response.ResponseObject.Note.Text);
        }

        [Theory]
        [InlineData("generalNote3", "gn3")]
        [InlineData(@"subject1\generalNote2", "This is generel note 2")]
        public async Task GetNote_RealPathWithoutExtension_NoteFound(string noteRelativePath, string expectedText)
        {
            IOptions<DatabaseConfigurtaion> databaseOptions = Options.Create(new DatabaseConfigurtaion()
            {
                NotesDirectoryPath = GeneralNotesDirectoryPath,
                NotesTasksDirectoryPath = "a"
            });

            string expectedNotePath = Path.Combine(GeneralNotesDirectoryPath, noteRelativePath) + ".txt";

            INoteService noteService = new NoteService(
                mNoteFactory, databaseOptions, NullLogger<NoteService>.Instance);
            IResponse<NoteResource> response = await noteService.GetGeneralNote(noteRelativePath).ConfigureAwait(false);

            Assert.True(response.IsSuccess);
            Assert.Equal(expectedNotePath, response.ResponseObject.Note.NotePath);
            Assert.Equal(expectedText, response.ResponseObject.Note.Text);
        }

        [Fact]
        public async Task GetNote_NoteNotExists_NoteNotFound()
        {
            IOptions<DatabaseConfigurtaion> databaseOptions = Options.Create(new DatabaseConfigurtaion()
            {
                NotesDirectoryPath = GeneralNotesDirectoryPath,
                NotesTasksDirectoryPath = "a"
            });

            const string noteName = "not_real_note.txt";
            string noteRelativePath = Path.Combine(GeneralNotesDirectoryName, noteName);

            INoteService noteService = new NoteService(
                mNoteFactory, databaseOptions, NullLogger<NoteService>.Instance);
            IResponse<NoteResource> response = await noteService.GetGeneralNote(noteRelativePath).ConfigureAwait(false);

            Assert.False(response.IsSuccess);
        }
    }
}