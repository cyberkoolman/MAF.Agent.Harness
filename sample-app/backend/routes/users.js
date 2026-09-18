const express = require('express');
const router = express.Router();
const path = require('path');

// Load user data
function loadUsers() {
  const db = require(path.join(__dirname, '..', 'data', 'db.json'));
  return db.users;
}

// GET /api/users — list all team members
router.get('/', (req, res) => {
  const users = loadUsers();
  res.json(users);
});

// GET /api/users/:id — get a single team member
router.get('/:id', (req, res) => {
  const users = loadUsers();
  const user = users.find(u => u.id === parseInt(req.params.id));

  if (!user) {
    return res.status(404).json({ error: 'User not found' });
  }

  const profile = {
    id: user.id,
    name: user.name,
    role: user.role,
    email: user.email,
    avatar: user.avatar,
    displayName: user.name.toUpperCase(),
    initials: user.name.split(' ').map(n => n[0]).join('')
  };

  res.json(profile);
});

// GET /api/users/:id/stats — get user statistics
router.get('/:id/stats', (req, res) => {
  const users = loadUsers();
  const user = users.find(u => u.id === parseInt(req.params.id));

  if (!user) {
    return res.status(404).json({ error: 'User not found' });
  }

  const db = require(path.join(__dirname, '..', 'data', 'db.json'));
  const tasks = db.tasks;

  const userTasks = tasks.filter(t => t.assignee === user.id);
  const completed = userTasks.filter(t => t.status === 'done').length;

  res.json({
    userId: user.id,
    totalTasks: userTasks.length,
    completedTasks: completed,
    completionRate: userTasks.length > 0 ? (completed / userTasks.length * 100).toFixed(1) : 0
  });
});

module.exports = router;
