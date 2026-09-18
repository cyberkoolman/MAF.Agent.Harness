import React from 'react';

function TaskBoard({ tasks }) {
  const columns = [
    { id: 'open', label: 'Open', color: '#2196F3' },
    { id: 'blocked', label: 'Blocked', color: '#FF9800' },
    { id: 'in-progress', label: 'In Progress', color: '#9C27B0' },
    { id: 'done', label: 'Done', color: '#4CAF50' },
  ];

  const priorityColors = {
    critical: '#F44336',
    high: '#FF9800',
    medium: '#2196F3',
    low: '#4CAF50',
  };

  return (
    <div className="task-board">
      <h2>Task Board</h2>

      <div className="board-columns">
        {columns.map(col => {
          const colTasks = tasks.filter(t => t.status === col.id);
          return (
            <div key={col.id} className="board-column">
              <div className="column-header" style={{ borderTopColor: col.color }}>
                <span className="column-title">{col.label}</span>
                <span className="column-count">{colTasks.length}</span>
              </div>
              <div className="column-cards">
                {colTasks.length === 0 ? (
                  <div className="empty-column">No tasks</div>
                ) : (
                  colTasks.map(task => (
                    <div key={task.id} className="task-card">
                      <div className="task-priority"
                           style={{ backgroundColor: priorityColors[task.priority] || '#999' }}>
                        {task.priority}
                      </div>
                      <div className="task-title">{task.title}</div>
                      <div className="task-assignee">{task.assigneeName}</div>
                    </div>
                  ))
                )}
              </div>
            </div>
          );
        })}
      </div>
    </div>
  );
}

export default TaskBoard;
