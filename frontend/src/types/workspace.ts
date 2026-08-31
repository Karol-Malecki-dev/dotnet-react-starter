export interface WorkspaceSearchResult {
  type: 'projectTask';
  resourceId: string;
  projectId: string;
  title: string;
  context: string;
}

export interface WorkspaceSearchPage {
  items: WorkspaceSearchResult[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface WorkspaceSearchResponse {
  statusCode: number;
  message: string;
  data: WorkspaceSearchPage;
}