import { AppConfiguration } from './index'

export const appConfig: AppConfiguration = {
  name: 'The Rift',
  version: '1.0.0',
  description: 'A comprehensive platform for collaborative development, project management, and team communication',
  features: {
    chat: true,
    projects: true,
    analytics: true,
    notifications: true,
    integrations: true
  },
  settings: {
    defaultTheme: 'system',
    enableAutoSave: true,
    enableRealtimeCollaboration: true,
    maxHistoryDays: 30
  }
}
