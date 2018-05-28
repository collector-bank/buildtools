//**********************************************************

#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <malloc.h>

char *gettcprop(const char *propname);
char *getpropvalue(const char *filename, const char *valuename);
void log(const char *message);

//**********************************************************

int main(int argc, char *argv[])
{
	char logbuf[1000];

	char *teamcity_build_branch = gettcprop("teamcity.build.branch");
	char *vcsroot_branch = gettcprop("vcsroot.branch");

	char *branchname;
	if (teamcity_build_branch)
	{
		sprintf(logbuf, "Found teamcity.build.branch: '%s'", teamcity_build_branch);
		log(logbuf);
		branchname = teamcity_build_branch;
	}
	else if (vcsroot_branch)
	{
		sprintf(logbuf, "Found vcsroot.branch: '%s'", vcsroot_branch);
		log(logbuf);
		branchname = vcsroot_branch;
	}
	else
	{
		log("Couldn't find any branch name.");
		return 0;
	}


	if (!strcmp(branchname, "master") || !strcmp(branchname, "refs/heads/master"))
	{
		sprintf(logbuf, "On master branch: '%s', keeping build number.", branchname);
		log(logbuf);
	}
	else
	{
		char *buildcounter = gettcprop("build.counter");
		if (buildcounter)
		{
			sprintf(logbuf, "Found build.counter: '%s'", buildcounter);
			log(logbuf);
		}
		else
		{
			log("Couldn't find any build counter.");
			return 0;
		}


		char buildnumber[1000];
		sprintf(buildnumber, "buildnumber = 0.0.%s", buildcounter);
		sprintf(logbuf, "Setting build number: '%s'", buildnumber);
		log(logbuf);

		if (argc < 2 || strcmp(argv[1], "-dryrun"))
		{
			sprintf(logbuf, "##teamcity[buildNumber '%s']", buildnumber);
			log(logbuf);
		}
	}

	return 0;
}

//**********************************************************

char *gettcprop(const char *propname)
{
	char logbuf[1000];

	char *buildpropfile = getenv("TEAMCITY_BUILD_PROPERTIES_FILE");
	if (!buildpropfile || !*buildpropfile)
	{
		log("Couldn't find Teamcity build properties file.");
		return NULL;
	}

	char *configpropfile = getpropvalue(buildpropfile, "teamcity.configuration.properties.file");
	if (!configpropfile)
	{
		sprintf(logbuf, "Couldn't find Teamcity build property: '%s'", "teamcity.configuration.properties.file");
		log(logbuf);
		return NULL;
	}

	char *propvalue = getpropvalue(configpropfile, propname);
	if (!propvalue)
	{
		sprintf(logbuf, "Couldn't find Teamcity config property: '%s'", propname);
		log(logbuf);
		return NULL;
	}

	return propvalue;
}

//**********************************************************

char *getpropvalue(const char *filename, const char *valuename)
{
	char logbuf[1000];
	FILE *fh;

	sprintf(logbuf, "Reading Teamcity properties file: '%s'", filename);
	log(logbuf);

	fh = fopen(filename, "rb");
	if (!fh)
	{
		log("Couldn't open Teamcity properties file.");
		return NULL;
	}

	fseek(fh, 0, SEEK_END);
	int bufsize = ftell(fh) + 1;

	char *buf = (char*)malloc(bufsize);

	fseek(fh, 0, SEEK_SET);
	fread(buf, bufsize - 1, 1, fh);
	buf[bufsize - 1] = 0;
	fclose(fh);

	int valuenamesize = strlen(valuename);

	char *p2;

	for (char *p = p2 = buf; p < buf + bufsize - 1; p++)
	{
		if (*p == '\n' || *p == '\r' || *p == 0)
		{
			*p = 0;
			char *p3 = strchr(p2, '=');
			if (p3)
			{
				*p3 = 0;
				if (!strcmp(p2, valuename))
				{
					for (p = p2 = p3 + 1; *p; p++)
					{
						if (*p == '\\' && (*(p + 1) == '\\' || *(p + 1) == ':'))
						{
							p++;
						}
						*p2++ = *p;
					}
					*p = 0;

					return p3 + 1;
				}
			}

			p2 = p + 1;
		}
	}

	return NULL;
}

//**********************************************************

void log(const char *message)
{
	printf("%s\n", message);
}

//**********************************************************
