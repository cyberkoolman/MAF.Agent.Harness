import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import App from '../App';

global.IS_REACT_ACT_ENVIRONMENT = true;

const users = [
  { id: 1, name: 'Phoenix', role: 'Lead', email: 'phoenix@example.test', avatar: '' },
];
const tasks = [
  { id: 1, title: 'Verify repair', status: 'done', priority: 'high', assigneeName: 'Phoenix' },
];

async function renderApp(responses) {
  global.fetch = jest.fn()
    .mockResolvedValueOnce(responses.users)
    .mockResolvedValueOnce(responses.tasks);

  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);

  await act(async () => {
    root.render(<App />);
  });

  return { container, root };
}

async function cleanup({ container, root }) {
  await act(async () => {
    root.unmount();
  });
  container.remove();
}

describe('App', () => {
  afterEach(() => {
    jest.restoreAllMocks();
  });

  it('loads API data and renders the dashboard', async () => {
    const rendered = await renderApp({
      users: { ok: true, json: async () => users },
      tasks: { ok: true, json: async () => tasks },
    });

    expect(rendered.container.textContent).toContain('Mission Control');
    expect(rendered.container.textContent).toContain('Team Members');
    expect(rendered.container.textContent).toContain('Completed');
    expect(global.fetch).toHaveBeenCalledWith('/api/users');
    expect(global.fetch).toHaveBeenCalledWith('/api/tasks');

    await cleanup(rendered);
  });

  it('shows an error when an API request fails', async () => {
    const rendered = await renderApp({
      users: { ok: false, status: 500, json: async () => ({}) },
      tasks: { ok: true, json: async () => tasks },
    });

    expect(rendered.container.textContent).toContain('Error: Users API failed: 500');

    await cleanup(rendered);
  });
});
