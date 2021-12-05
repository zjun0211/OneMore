﻿//************************************************************************************************
// Copyright © 2021 Steven M Cohn.  All rights reserved.
//************************************************************************************************

namespace River.OneMoreAddIn.Commands
{
	using River.OneMoreAddIn.Helpers.Office;
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System.Xml.Linq;


	internal class ImportOutlookTasksCommand : Command
	{

		public ImportOutlookTasksCommand()
		{
		}


		public override async Task Execute(params object[] args)
		{
			IEnumerable<OutlookTask> tasks;
			using (var outlook = new Outlook())
			{
				var folders = outlook.GetTaskHierarchy();

				using (var dialog = new ImportOutlookTasksDialog(folders))
				{
					if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
					{
						return;
					}
				}

				tasks = Flatten(folders)
					.Where(t => string.IsNullOrEmpty(t.OneNoteTaskID));

				if (!tasks.Any())
				{
					return;
				}

				var list = tasks.ToList();

				using (var one = new OneNote(out var page, out var ns))
				{
					foreach (var task in list)
					{
						task.OneNoteTaskID = Guid.NewGuid().ToString("b").ToUpper();

						page.AddNextParagraph(
							new XElement(ns + "OE",
								new XElement(ns + "OutlookTask",
									new XAttribute("startDate", task.CreationTime.ToZuluString()),
									new XAttribute("dueDate", task.DueDate.ToZuluString()),
									new XAttribute("guidTask", task.OneNoteTaskID),
									new XAttribute("completed", task.Complete.ToString().ToLower()),
									new XAttribute("creationDate", task.CreationTime.ToZuluString())
									),
								new XElement(ns + "T",
									new XCData(task.Subject))
							));
					}

					await one.Update(page);

					// re-fetch page to get IDs of new paragraphs...
					page = one.GetPage(page.PageId, OneNote.PageDetail.Basic);
					ns = page.Namespace;

					foreach (var task in list)
					{
						var paragraph = page.Root.Descendants(ns + "OutlookTask")
							.Where(e => e.Attribute("guidTask").Value == task.OneNoteTaskID)
							.Select(e => e.Parent)
							.FirstOrDefault();

						if (paragraph != null)
						{
							var id = paragraph.Attribute("objectID").Value;
							task.OneNoteURL = one.GetHyperlink(page.PageId, id);

							outlook.SaveTask(task);
						}
					}
				}
			}
		}


		private IEnumerable<OutlookTask> Flatten(OutlookTaskFolders folders)
		{
			foreach (var folder in folders)
			{
				foreach (var task in folder.Tasks)
				{
					yield return task;
				}

				foreach (var t in Flatten(folder.Folders))
				{
					yield return t;
				}
			}
		}
	}
}
