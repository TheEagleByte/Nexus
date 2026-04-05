"use client";

import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ProjectsList } from "@/components/spoke-detail/projects-list";
import { JobsList } from "@/components/spoke-detail/jobs-list";
import type { ProjectResponse, JobResponse } from "@/types/api";

interface SpokeDetailTabsProps {
  projects: ProjectResponse[];
  jobs: JobResponse[];
}

export function SpokeDetailTabs({ projects, jobs }: SpokeDetailTabsProps) {
  return (
    <Tabs defaultValue="chat" className="w-full">
      <TabsList className="w-full justify-start border-b border-border bg-transparent rounded-none px-4">
        <TabsTrigger
          value="chat"
          className="data-[state=active]:text-primary data-[state=active]:border-b-2 data-[state=active]:border-primary rounded-none"
        >
          Chat
        </TabsTrigger>
        <TabsTrigger
          value="jobs"
          className="data-[state=active]:text-primary data-[state=active]:border-b-2 data-[state=active]:border-primary rounded-none"
        >
          Jobs
        </TabsTrigger>
        <TabsTrigger
          value="projects"
          className="data-[state=active]:text-primary data-[state=active]:border-b-2 data-[state=active]:border-primary rounded-none"
        >
          Projects
        </TabsTrigger>
      </TabsList>
      <TabsContent value="chat">
        <div className="p-6 text-center">
          <div className="text-2xl mb-2 font-mono text-muted-foreground">
            {">"}_
          </div>
          <p className="text-sm text-muted-foreground">
            Conversation panel coming soon
          </p>
        </div>
      </TabsContent>
      <TabsContent value="jobs">
        <JobsList jobs={jobs} />
      </TabsContent>
      <TabsContent value="projects">
        <ProjectsList projects={projects} />
      </TabsContent>
    </Tabs>
  );
}
