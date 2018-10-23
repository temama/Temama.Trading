import { BrowserModule } from '@angular/platform-browser';
import { NgModule, ApplicationRef, ErrorHandler } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClientModule } from '@angular/common/http';
import { RouterModule } from '@angular/router';
import { ModalModule } from 'ngx-bootstrap/modal';

import { ErrorMessageComponent } from './error-message/error-message.component';
import { ErrorModalComponent } from './error-message/error-message.component';
import { AppComponent } from './app.component';
import { NavMenuComponent } from './nav-menu/nav-menu.component';
import { StatsComponent } from './stats/stats.component';
import { BotsComponent } from './bots/bots.component';
import { ExchangesComponent } from './exchanges/exchanges.component';
import { NotifsComponent } from './notifs/notifs.component';
import { SettingsComponent } from './settings/settings.component';
import { AppLogComponent } from './app-log/app-log.component';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    StatsComponent,
    BotsComponent,
    ExchangesComponent,
    NotifsComponent,
    SettingsComponent,
    AppLogComponent,
    ErrorModalComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    HttpClientModule,
    FormsModule,
    RouterModule.forRoot([
      { path: '', redirectTo: 'stats', pathMatch: 'full' },
      { path: 'stats', component: StatsComponent },
      { path: 'bots', component: BotsComponent },
      { path: 'exchanges', component: ExchangesComponent },
      { path: 'notifs', component: NotifsComponent },
      { path: 'settings', component: SettingsComponent },
      { path: 'app-log', component: AppLogComponent }
    ]),
    ModalModule.forRoot()
  ],
  providers: [
    {
      provide: ErrorHandler,
      useClass: ErrorMessageComponent
    }
  ],
  entryComponents: [
    ErrorModalComponent
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
