"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
var stats_component_1 = require("./stats/stats.component");
var exchanges_component_1 = require("./exchanges/exchanges.component");
var notifs_component_1 = require("./notifs/notifs.component");
var bots_component_1 = require("./bots/bots.component");
var settings_component_1 = require("./settings/settings.component");
var app_log_component_1 = require("./app-log/app-log.component");
var login_component_1 = require("./login/login.component");
var auth_guard_1 = require("./login/auth.guard");
exports.appRoutes = [
    { path: '', redirectTo: 'stats', pathMatch: 'full', canActivate: [auth_guard_1.AuthGuard] },
    { path: 'stats', component: stats_component_1.StatsComponent, canActivate: [auth_guard_1.AuthGuard] },
    { path: 'bots', component: bots_component_1.BotsComponent, canActivate: [auth_guard_1.AuthGuard] },
    { path: 'exchanges', component: exchanges_component_1.ExchangesComponent, canActivate: [auth_guard_1.AuthGuard] },
    { path: 'notifs', component: notifs_component_1.NotifsComponent, canActivate: [auth_guard_1.AuthGuard] },
    { path: 'settings', component: settings_component_1.SettingsComponent, canActivate: [auth_guard_1.AuthGuard] },
    { path: 'app-log', component: app_log_component_1.AppLogComponent, canActivate: [auth_guard_1.AuthGuard] },
    { path: 'login', component: login_component_1.LoginComponent }
];
//# sourceMappingURL=routes.js.map