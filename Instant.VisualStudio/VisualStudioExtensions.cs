//
// VisualStudioExtensions.cs
//
// Copyright 2012 Eric Maupin
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at

//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.IO;
using Cadenza;
using EnvDTE;
using VSLangProj;

namespace Instant.VisualStudio
{
	public static class VisualStudioExtensions
	{
		private const string PhysicalFileKind = "{6BB5F8EE-4483-11D3-8BCF-00C04F8EC28C}";
		private const string PhysicalFolderKind = "{6BB5F8EF-4483-11D3-8BCF-00C04F8EC28C}";
		private const string VirtualFolderKind = "{6BB5F8F0-4483-11D3-8BCF-00C04F8EC28C}";

		public static IProject GetProject (this _DTE dte, Document currentDoc, string code)
		{
			Project instantProject = new Project();
			instantProject.Sources.Add (Either<FileInfo, string>.B (code));

			EnvDTE.Project project = currentDoc.ProjectItem.ContainingProject;

			EnvDTE.Properties properties = project.ConfigurationManager.ActiveConfiguration.Properties;

			instantProject.DefinedConstants = (string)properties.Item ("DefineConstants").Value;
			instantProject.AllowUnsafe = (bool)properties.Item ("AllowUnsafeBlocks").Value;
			instantProject.Optimize = (bool)properties.Item ("Optimize").Value;

			AddFiles (instantProject, project.ProjectItems, currentDoc);
			AddReferences (instantProject, ((VSProject)project.Object).References);

			return instantProject;
		}

		private static void AddReferences (Project instantProject, References references)
		{
			foreach (Reference reference in references)
			{
				if (reference.SourceProject == null)
				{
					if (Path.GetFileName (reference.Path) == "mscorlib.dll")
						continue; // mscorlib is added automatically

					instantProject.References.Add (reference.Path);
				}
				else
				{
					instantProject.References.Add (GetOutputPath (reference.SourceProject));
					AddReferences (instantProject, ((VSProject)reference.SourceProject.Object).References);
				}
			}
		}

		private static void AddFiles (Project project, ProjectItems items, Document currentDoc)
		{
			foreach (ProjectItem subItem in items)
			{
				if (currentDoc == subItem)
					continue;

				if (subItem.Kind == PhysicalFolderKind || subItem.Kind == VirtualFolderKind)
					AddFiles (project, subItem.ProjectItems, currentDoc);
				else if (subItem.Kind == PhysicalFileKind)
				{
					if (subItem.Name.EndsWith (".cs")) // HACK: Gotta be a better way to know if it's C#.
					{
						for (short i = 0; i < subItem.FileCount; i++)
						{
							string path = subItem.FileNames[i];
							if (path == currentDoc.FullName)
								continue;

							project.Sources.Add (Either<FileInfo, string>.A (new FileInfo (path)));
						}
					}
				}
			}
		}

		public static string GetOutputPath (this EnvDTE.Project project)
		{
			if (project == null)
				throw new ArgumentNullException ("project");

			FileInfo csproj = new FileInfo (project.FullName);

			string outputPath = (string)project.ConfigurationManager.ActiveConfiguration.Properties.Item ("OutputPath").Value;
			string file = (string)project.Properties.Item ("OutputFileName").Value;

			return Path.Combine (csproj.Directory.FullName, outputPath, file);
		}
	}
}