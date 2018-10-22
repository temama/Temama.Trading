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
import { AppLogComponent } from './app-log/app-log.component';

@NgModule({
  declarations: [
    AppComponent,
    NavMenuComponent,
    AppLogComponent,
    ErrorModalComponent
  ],
  imports: [
    BrowserModule.withServerTransition({ appId: 'ng-cli-universal' }),
    HttpClientModule,
    FormsModule,
    RouterModule.forRoot([
      { path: '', component: AppLogComponent, pathMatch: 'full' },
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
