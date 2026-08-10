const DashboardCharts = (function () {
    let lineChart = null;
    let pieChart = null;
    let currentPendingPage = 1;
    let activityCursor = null;

    async function init() {
        await loadDashboardStats();
    }

    async function loadDashboardStats() {
        try {
            const response = await fetch('/api/v1/dashboard/stats');
            if (!response.ok) {
                console.error('Failed to load dashboard stats:', response.status);
                return;
            }

            const stats = await response.json();
            populateKPIs(stats);
            initializeCharts(stats);
            renderActivityFeed(stats.recentActivity);
            renderPendingActions(stats.pendingActions);
            activityCursor = stats.recentActivity.nextCursor;
        } catch (error) {
            console.error('Error loading dashboard stats:', error);
        }
    }

    function populateKPIs(stats) {
        document.getElementById('totalComplaints').textContent = stats.totalComplaints;
        document.getElementById('openCount').textContent = stats.openCount;
        document.getElementById('inProgressCount').textContent = stats.inProgressCount;
        document.getElementById('resolvedCount').textContent = stats.resolvedCount;
    }

    function initializeCharts(stats) {
        initializeLineChart(stats.complaintsOverTime);
        initializePieChart(stats.complaintsByCategory);
    }

    function initializeLineChart(data) {
        const ctx = document.getElementById('complaintsLineChart');
        if (!ctx) return;

        // Fill gaps in data (last 30 days)
        const dates = generateLast30Days();
        const dataMap = new Map(data.map(d => [new Date(d.date).toDateString(), d.count]));
        const filledData = dates.map(date => dataMap.get(date.toDateString()) || 0);

        lineChart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: dates.map(d => d.toLocaleDateString('en-US', { month: 'short', day: 'numeric' })),
                datasets: [
                    {
                        label: 'Complaints',
                        data: filledData,
                        borderColor: '#0d6efd',
                        backgroundColor: 'rgba(13, 110, 253, 0.1)',
                        borderWidth: 2,
                        fill: true,
                        tension: 0.4,
                        pointRadius: 3,
                        pointBackgroundColor: '#0d6efd',
                        pointBorderColor: '#fff',
                        pointBorderWidth: 2,
                        pointHoverRadius: 5
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        display: true,
                        position: 'top',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 12, weight: '500' }
                        }
                    },
                    filler: {
                        propagate: true
                    }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        ticks: {
                            stepSize: 1,
                            font: { size: 11 }
                        },
                        grid: {
                            color: 'rgba(0, 0, 0, 0.05)'
                        }
                    },
                    x: {
                        ticks: {
                            font: { size: 11 }
                        },
                        grid: {
                            display: false
                        }
                    }
                }
            }
        });
    }

    function initializePieChart(data) {
        const ctx = document.getElementById('categoryPieChart');
        if (!ctx) return;

        const colors = data.map(d => d.color);
        const labels = data.map(d => d.category);
        const counts = data.map(d => d.count);

        pieChart = new Chart(ctx, {
            type: 'doughnut',
            data: {
                labels: labels,
                datasets: [
                    {
                        data: counts,
                        backgroundColor: colors,
                        borderColor: '#fff',
                        borderWidth: 2,
                        hoverOffset: 10
                    }
                ]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: {
                            usePointStyle: true,
                            padding: 15,
                            font: { size: 12, weight: '500' }
                        }
                    },
                    tooltip: {
                        callbacks: {
                            label: function (context) {
                                const total = context.dataset.data.reduce((a, b) => a + b, 0);
                                const percentage = ((context.parsed * 100) / total).toFixed(1);
                                return `${context.label}: ${context.parsed} (${percentage}%)`;
                            }
                        }
                    }
                }
            }
        });
    }

    function renderActivityFeed(activityResult) {
        const container = document.getElementById('activityFeed');
        if (!container) return;

        const activities = activityResult.items || [];

        if (activities.length === 0) {
            container.innerHTML = '<div class="text-center text-muted py-4">No recent activity</div>';
            return;
        }

        const html = activities
            .map(activity => {
                const time = new Date(activity.timestamp);
                const timeStr = formatTime(time);

                return `
                <div class="activity-item">
                    <div class="d-flex justify-content-between mb-1">
                        <div class="activity-action">${escapeHtml(activity.action)}</div>
                        <span class="activity-time">${timeStr}</span>
                    </div>
                    <div class="activity-description">${escapeHtml(activity.description)}</div>
                    <div style="font-size: 0.8rem; color: #999; margin-top: 0.25rem;">by ${escapeHtml(activity.initiatedBy)}</div>
                </div>
            `;
            })
            .join('');

        const loadMoreHtml = activityResult.hasMore
            ? `<button class="btn btn-sm btn-outline-secondary w-100 mt-2" onclick="DashboardCharts.loadMoreActivity()">Load More</button>`
            : '';

        container.innerHTML = html + loadMoreHtml;
    }

    async function loadMoreActivity() {
        if (!activityCursor) return;

        try {
            const response = await fetch(`/api/v1/dashboard/recent-activity?cursor=${encodeURIComponent(activityCursor)}`);
            if (!response.ok) return;

            const result = await response.json();
            activityCursor = result.nextCursor;

            const container = document.getElementById('activityFeed');
            const existingButton = container.querySelector('button');
            if (existingButton) existingButton.remove();

            const html = result.items
                .map(activity => {
                    const time = new Date(activity.timestamp);
                    const timeStr = formatTime(time);

                    return `
                    <div class="activity-item">
                        <div class="d-flex justify-content-between mb-1">
                            <div class="activity-action">${escapeHtml(activity.action)}</div>
                            <span class="activity-time">${timeStr}</span>
                        </div>
                        <div class="activity-description">${escapeHtml(activity.description)}</div>
                        <div style="font-size: 0.8rem; color: #999; margin-top: 0.25rem;">by ${escapeHtml(activity.initiatedBy)}</div>
                    </div>
                `;
                })
                .join('');

            const loadMoreHtml = result.hasMore
                ? `<button class="btn btn-sm btn-outline-secondary w-100 mt-2" onclick="DashboardCharts.loadMoreActivity()">Load More</button>`
                : '';

            container.insertAdjacentHTML('beforeend', html + loadMoreHtml);
        } catch (error) {
            console.error('Error loading more activity:', error);
        }
    }

    function renderPendingActions(pagedResult) {
        const container = document.getElementById('pendingActions');
        if (!container) return;

        const actions = pagedResult.items || [];

        if (actions.length === 0) {
            container.innerHTML = '<div class="text-center text-muted py-4">No pending actions</div>';
            return;
        }

        const html = actions
            .map(action => {
                const statusColor = getStatusColor(action.status);
                const daysLabel = action.daysPending === 1 ? 'day' : 'days';

                return `
                <div class="pending-item">
                    <div class="pending-header">
                        <span class="pending-title">${escapeHtml(action.title)}</span>
                        <span class="badge pending-badge" style="background-color: ${statusColor};">${escapeHtml(action.status)}</span>
                    </div>
                    <div class="pending-meta">
                        <span>${escapeHtml(action.studentName)}</span>
                        <span>${escapeHtml(action.category)}</span>
                        <span class="pending-days">${action.daysPending} ${daysLabel}</span>
                    </div>
                </div>
            `;
            })
            .join('');

        const prevDisabled = !pagedResult.hasPreviousPage ? 'disabled' : '';
        const nextDisabled = !pagedResult.hasNextPage ? 'disabled' : '';

        const pagerHtml = `
        <div class="d-flex justify-content-between align-items-center mt-2">
            <button class="btn btn-sm btn-outline-secondary" ${prevDisabled} onclick="DashboardCharts.changePendingPage(${pagedResult.pageNumber - 1})">Previous</button>
            <span style="font-size: 0.85rem; color: #666;">Page ${pagedResult.pageNumber} of ${pagedResult.totalPages}</span>
            <button class="btn btn-sm btn-outline-secondary" ${nextDisabled} onclick="DashboardCharts.changePendingPage(${pagedResult.pageNumber + 1})">Next</button>
        </div>
    `;

        container.innerHTML = html + pagerHtml;
    }

    async function changePendingPage(page) {
        if (page < 1) return;

        try {
            const response = await fetch(`/api/v1/dashboard/pending-actions?page=${page}`);
            if (!response.ok) return;

            const result = await response.json();
            currentPendingPage = page;
            renderPendingActions(result);
        } catch (error) {
            console.error('Error changing pending actions page:', error);
        }
    }

    function generateLast30Days() {
        const dates = [];
        for (let i = 29; i >= 0; i--) {
            const d = new Date();
            d.setDate(d.getDate() - i);
            dates.push(new Date(d.getFullYear(), d.getMonth(), d.getDate()));
        }
        return dates;
    }

    function formatTime(date) {
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 1) return 'just now';
        if (diffMins < 60) return `${diffMins}m ago`;
        if (diffHours < 24) return `${diffHours}h ago`;
        if (diffDays < 7) return `${diffDays}d ago`;

        return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
    }

    function getStatusColor(status) {
        const colors = {
            'Open': '#dc3545',
            'InProgress': '#ffc107',
            'Resolved': '#198754',
            'Closed': '#6c757d'
        };
        return colors[status] || '#6c757d';
    }

    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }

    document.addEventListener('DOMContentLoaded', init);

    return { init, loadMoreActivity, changePendingPage };
})();
