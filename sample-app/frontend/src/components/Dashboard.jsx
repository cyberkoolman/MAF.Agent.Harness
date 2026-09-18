import React from 'react';

function Dashboard({ users, tasks }) {
  const openTasks = tasks.filter(t => t.status === 'open').length;
  const blockedTasks = tasks.filter(t => t.status === 'blocked').length;
  const doneTasks = tasks.filter(t => t.status === 'done').length;

  return (
    <div className="dashboard">
      <h2>Dashboard</h2>

      <div className="stats-grid">
        <div className="stat-card">
          <div className="stat-number">{users.length}</div>
          <div className="stat-label">Team Members</div>
        </div>
        <div className="stat-card stat-open">
          <div className="stat-number">{openTasks}</div>
          <div className="stat-label">Open Tasks</div>
        </div>
        <div className="stat-card stat-blocked">
          <div className="stat-number">{blockedTasks}</div>
          <div className="stat-label">Blocked</div>
        </div>
        <div className="stat-card stat-done">
          <div className="stat-number">{doneTasks}</div>
          <div className="stat-label">Completed</div>
        </div>
      </div>

      <div className="recent-activity">
        <h3>Recent Activity</h3>
        <div className="activity-list">
          <div className="activity-item">
            <span className="activity-time">2 min ago</span>
            <span className="activity-text">Phoenix created task "Deploy to staging"</span>
          </div>
          <div className="activity-item">
            <span className="activity-time">5 min ago</span>
            <span className="activity-text">Sentinel reported: regression tests passing</span>
          </div>
          <div className="activity-item">
            <span className="activity-time">8 min ago</span>
            <span className="activity-text">Forge verified missing users return HTTP 404</span>
          </div>
          <div className="activity-item">
            <span className="activity-time">12 min ago</span>
            <span className="activity-text">Ember restored the Navbar and browser rendering</span>
          </div>
        </div>
      </div>
    </div>
  );
}

export default Dashboard;
