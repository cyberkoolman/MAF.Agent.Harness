import React, { useState, useEffect } from 'react';
import Navbar from './components/Navbar';
import Dashboard from './components/Dashboard';
import UserList from './components/UserList';
import TaskBoard from './components/TaskBoard';
import Footer from './components/Footer';
import './App.css';

function App() {
  const [currentPage, setCurrentPage] = useState('dashboard');
  const [users, setUsers] = useState([]);
  const [tasks, setTasks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    async function fetchData() {
      try {
        setLoading(true);
        const [usersRes, tasksRes] = await Promise.all([
          fetch('/api/users'),
          fetch('/api/tasks')
        ]);

        if (!usersRes.ok) throw new Error(`Users API failed: ${usersRes.status}`);
        if (!tasksRes.ok) throw new Error(`Tasks API failed: ${tasksRes.status}`);

        setUsers(await usersRes.json());
        setTasks(await tasksRes.json());
        setError(null);
      } catch (err) {
        setError(err.message);
      } finally {
        setLoading(false);
      }
    }
    fetchData();
  }, []);

  const navItems = [
    { id: 'dashboard', label: 'Dashboard' },
    { id: 'team', label: 'Team' },
    { id: 'tasks', label: 'Tasks' },
  ];

  const renderPage = () => {
    if (loading) return <div className="loading">Loading...</div>;
    if (error) return <div className="error-banner">Error: {error}</div>;

    switch (currentPage) {
      case 'dashboard':
        return <Dashboard users={users} tasks={tasks} />;
      case 'team':
        return <UserList users={users} />;
      case 'tasks':
        return <TaskBoard tasks={tasks} />;
      default:
        return <Dashboard users={users} tasks={tasks} />;
    }
  };

  return (
    <div className="app">
      <Navbar
        items={navItems}
        currentPage={currentPage}
        onNavigate={setCurrentPage}
      />
      <main className="main-content">
        {renderPage()}
      </main>
      <Footer />
    </div>
  );
}

export default App;
