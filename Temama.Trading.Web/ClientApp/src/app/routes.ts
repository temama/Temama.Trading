import { Routes } from "@angular/router";
import { StatsComponent } from "./stats/stats.component";
import { ExchangesComponent } from "./exchanges/exchanges.component";
import { NotifsComponent } from "./notifs/notifs.component";
import { BotsComponent } from "./bots/bots.component";
import { SettingsComponent } from "./settings/settings.component";
import { AppLogComponent } from "./app-log/app-log.component";
import { LoginComponent } from "./login/login.component";
import { AuthGuard } from "./login/auth.guard";

export const appRoutes: Routes = [
  { path: '', redirectTo: 'stats', pathMatch: 'full', canActivate: [AuthGuard] },
  { path: 'stats', component: StatsComponent, canActivate: [AuthGuard] },
  { path: 'bots', component: BotsComponent, canActivate: [AuthGuard] },
  { path: 'exchanges', component: ExchangesComponent, canActivate: [AuthGuard] },
  { path: 'notifs', component: NotifsComponent, canActivate: [AuthGuard] },
  { path: 'settings', component: SettingsComponent, canActivate: [AuthGuard] },
  { path: 'app-log', component: AppLogComponent, canActivate: [AuthGuard] },
  { path: 'login', component: LoginComponent }
];
