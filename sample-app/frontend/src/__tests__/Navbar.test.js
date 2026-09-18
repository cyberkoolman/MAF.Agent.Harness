import React, { act } from 'react';
import { createRoot } from 'react-dom/client';
import Navbar from '../components/Navbar';

global.IS_REACT_ACT_ENVIRONMENT = true;

const mockItems = [
  { id: 'dashboard', label: 'Dashboard' },
  { id: 'team', label: 'Team' },
  { id: 'tasks', label: 'Tasks' },
];

function renderNavbar(props) {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);
  act(() => {
    root.render(<Navbar {...props} />);
  });
  return { container, root };
}

function cleanup({ container, root }) {
  act(() => root.unmount());
  container.remove();
}

describe('Navbar', () => {
  it('renders without crashing and maps the supplied items', () => {
    const { container, root } = renderNavbar({
      items: mockItems,
      currentPage: 'dashboard',
      onNavigate: () => {},
    });
    expect(container).toBeTruthy();
    cleanup({ container, root });
  });

  it('renders all nav item labels', () => {
    const { container, root } = renderNavbar({
      items: mockItems,
      currentPage: 'dashboard',
      onNavigate: () => {},
    });
    expect(container.textContent).toContain('Dashboard');
    expect(container.textContent).toContain('Team');
    expect(container.textContent).toContain('Tasks');
    cleanup({ container, root });
  });

  it('marks the current page as active', () => {
    const { container, root } = renderNavbar({
      items: mockItems,
      currentPage: 'team',
      onNavigate: () => {},
    });
    const listItems = container.querySelectorAll('li.navbar-link');
    const activeItems = Array.from(listItems).filter(li => li.classList.contains('active'));
    expect(activeItems.length).toBe(1);
    expect(activeItems[0].textContent).toContain('Team');
    cleanup({ container, root });
  });

  it('calls onNavigate with the correct id on click', () => {
    const onNavigate = jest.fn();
    const { container, root } = renderNavbar({
      items: mockItems,
      currentPage: 'dashboard',
      onNavigate,
    });
    const buttons = container.querySelectorAll('button');
    // Find the "Tasks" button and click it
    const tasksBtn = Array.from(buttons).find(b => b.textContent === 'Tasks');
    act(() => { tasksBtn.click(); });
    expect(onNavigate).toHaveBeenCalledWith('tasks');
    cleanup({ container, root });
  });

  it('renders the Mission Control brand title', () => {
    const { container, root } = renderNavbar({
      items: mockItems,
      currentPage: 'dashboard',
      onNavigate: () => {},
    });
    expect(container.textContent).toContain('Mission Control');
    cleanup({ container, root });
  });

  it('renders System Online status indicator', () => {
    const { container, root } = renderNavbar({
      items: mockItems,
      currentPage: 'dashboard',
      onNavigate: () => {},
    });
    expect(container.textContent).toContain('System Online');
    cleanup({ container, root });
  });
});
