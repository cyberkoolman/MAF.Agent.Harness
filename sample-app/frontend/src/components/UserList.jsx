import React, { useState } from 'react';

function UserList({ users }) {
  const [selectedUser, setSelectedUser] = useState(null);
  const [userStats, setUserStats] = useState(null);
  const [statsError, setStatsError] = useState(null);

  const handleUserClick = async (user) => {
    setSelectedUser(user);
    setStatsError(null);
    try {
      const res = await fetch(`/api/users/${user.id}/stats`);
      if (!res.ok) throw new Error(`Stats failed: ${res.status}`);
      setUserStats(await res.json());
    } catch (err) {
      setStatsError(err.message);
      setUserStats(null);
    }
  };

  return (
    <div className="user-list">
      <h2>Team Members</h2>

      <div className="user-grid">
        {users.map(user => (
          <div
            key={user.id}
            className={`user-card ${selectedUser?.id === user.id ? 'selected' : ''}`}
            onClick={() => handleUserClick(user)}
          >
            <img
              src={user.avatar}
              alt={user.name}
              className="user-avatar"
            />
            <div className="user-info">
              <h3 className="user-name">{user.name}</h3>
              <p className="user-role">{user.role}</p>
              <p className="user-email">{user.email}</p>
            </div>
          </div>
        ))}
      </div>

      {selectedUser && (
        <div className="user-detail-panel">
          <h3>{selectedUser.name} — Stats</h3>
          {statsError ? (
            <div className="error-banner">
              Failed to load stats: {statsError}
              <br />
              <small>The /api/users/{selectedUser.id}/stats endpoint may be broken.</small>
            </div>
          ) : userStats ? (
            <div className="stats-row">
              <div className="mini-stat">
                <span className="mini-stat-num">{userStats.totalTasks}</span>
                <span className="mini-stat-label">Total</span>
              </div>
              <div className="mini-stat">
                <span className="mini-stat-num">{userStats.completedTasks}</span>
                <span className="mini-stat-label">Done</span>
              </div>
              <div className="mini-stat">
                <span className="mini-stat-num">{userStats.completionRate}%</span>
                <span className="mini-stat-label">Rate</span>
              </div>
            </div>
          ) : (
            <div className="loading">Loading stats...</div>
          )}
        </div>
      )}
    </div>
  );
}

export default UserList;
